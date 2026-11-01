using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.Auth;

/// <summary>The signed-in user as the web app sees them.</summary>
public sealed record MeResponse(Guid Id, string Email, string Name, string? PictureUrl, EffectiveRole Role, string Language, Theme Theme);

/// <summary>What a sign-in or refresh returns: a 15-minute access token, the next refresh token and the user.</summary>
public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, MeResponse User);

/// <summary>
/// Sign-ins and their refresh tokens. A sign-in (one browser or device) is a session id; each refresh swaps the
/// token for a new one in the same session. A token that was already swapped or cancelled and is presented again
/// was copied, so the whole session ends (the person signs in again on that device). Expired tokens of a user are
/// deleted whenever that user signs in or refreshes, so the table doesn't grow without bound.
/// </summary>
public sealed partial class Sessions(AppDbContext db, AccessTokens accessTokens, IOptions<AuthOptions> options, TimeProvider time, ILogger<Sessions> logger)
{
    public async Task<SessionResponse> SignInAsync(GoogleIdentity identity, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == identity.Subject, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                GoogleSubject = identity.Subject,
                Email = identity.Email,
                DisplayName = identity.Name,
                CreatedAt = now,
            };
            db.Users.Add(user);
        }

        // Google is the source of the name, email and picture: refreshed at every sign-in.
        user.Email = identity.Email;
        user.DisplayName = identity.Name;
        user.PictureUrl = identity.PictureUrl;
        user.LastSignInAt = now;

        await DeleteExpiredAsync(user.Id, now, cancellationToken);
        var refreshToken = AddToken(user.Id, Guid.CreateVersion7(), now);
        await db.SaveChangesAsync(cancellationToken);
        return Response(user, refreshToken);
    }

    /// <summary>A new access token and the next refresh token, or null when the token can't be used (sign in again).</summary>
    public async Task<SessionResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!RefreshTokenValue.LooksValid(refreshToken))
        {
            return null;
        }

        var hash = RefreshTokenValue.Hash(refreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = time.GetUtcNow();
        if (stored is null || stored.ExpiresAt <= now)
        {
            return null;
        }

        if (stored.UsedAt is not null || stored.RevokedAt is not null)
        {
            LogReuse(logger, stored.UserId, stored.SessionId);
            await EndSessionAsync(stored.SessionId, now, cancellationToken);
            return null;
        }

        stored.UsedAt = now;
        await DeleteExpiredAsync(stored.UserId, now, cancellationToken);
        var next = AddToken(stored.UserId, stored.SessionId, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request used or cancelled this token at the same moment: treat it like a copy.
            db.ChangeTracker.Clear();
            LogReuse(logger, stored.UserId, stored.SessionId);
            await EndSessionAsync(stored.SessionId, now, cancellationToken);
            return null;
        }

        return Response(stored.User, next);
    }

    /// <summary>Ends the sign-in this refresh token belongs to (this device). Unknown tokens are ignored.</summary>
    public async Task SignOutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (!RefreshTokenValue.LooksValid(refreshToken))
        {
            return;
        }

        var hash = RefreshTokenValue.Hash(refreshToken);
        var sessionId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.SessionId)
            .SingleOrDefaultAsync(cancellationToken);
        if (sessionId is { } id)
        {
            await EndSessionAsync(id, time.GetUtcNow(), cancellationToken);
        }
    }

    /// <summary>Ends every sign-in of the user, on every device.</summary>
    public Task SignOutEverywhereAsync(Guid userId, CancellationToken cancellationToken) =>
        RevokeAsync(t => t.UserId == userId, time.GetUtcNow(), cancellationToken);

    private Task EndSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
        RevokeAsync(t => t.SessionId == sessionId, now, cancellationToken);

    private async Task RevokeAsync(System.Linq.Expressions.Expression<Func<RefreshToken, bool>> which, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Retried when a refresh changes one of the tokens at the same moment, so the new token is cancelled too.
        for (var attempt = 1; ; attempt++)
        {
            var live = await db.RefreshTokens.Where(which).Where(t => t.RevokedAt == null).ToListAsync(cancellationToken);
            live.ForEach(t => t.RevokedAt = now);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task DeleteExpiredAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expired = await db.RefreshTokens.Where(t => t.UserId == userId && t.ExpiresAt <= now).ToListAsync(cancellationToken);
        db.RefreshTokens.RemoveRange(expired);
    }

    private string AddToken(Guid userId, Guid sessionId, DateTimeOffset now)
    {
        var token = RefreshTokenValue.New();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = RefreshTokenValue.Hash(token),
            CreatedAt = now,
            ExpiresAt = now + options.Value.RefreshTokenLifetime,
        });
        return token;
    }

    private SessionResponse Response(User user, string refreshToken)
    {
        var me = Me(user, options.Value);
        var (accessToken, expiresAt) = accessTokens.Create(user.Id, user.Email, user.DisplayName, me.Role);
        return new SessionResponse(accessToken, expiresAt, refreshToken, me);
    }

    public static EffectiveRole RoleOf(User user, AuthOptions options) =>
        options.IsSuperAdmin(user.Email) ? EffectiveRole.SuperAdmin
        : user.Role == UserRole.Admin ? EffectiveRole.Admin
        : EffectiveRole.User;

    public static MeResponse Me(User user, AuthOptions options) =>
        new(user.Id, user.Email, user.DisplayName, user.PictureUrl, RoleOf(user, options), user.Language, user.Theme);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A used or cancelled refresh token was presented again; sign-in {SessionId} of user {UserId} ended")]
    private static partial void LogReuse(ILogger logger, Guid userId, Guid sessionId);
}
