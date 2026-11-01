using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class SessionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SignIn_NewAccount_CreatesTheUserAndOneToken()
    {
        using var kit = new AuthTestKit();

        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        var user = await kit.Db.Users.SingleAsync(Ct);
        Assert.Equal(("google-ash", "ash@example.com", "Ash", "https://lh3.googleusercontent.com/a/ash"),
            (user.GoogleSubject, user.Email, user.DisplayName, user.PictureUrl));
        Assert.Equal(kit.Clock.Now, user.CreatedAt);
        Assert.Equal(kit.Clock.Now, user.LastSignInAt);
        var token = await kit.Db.RefreshTokens.SingleAsync(Ct);
        Assert.Equal(RefreshTokenValue.Hash(session.RefreshToken), token.TokenHash);
        Assert.Equal(kit.Clock.Now.AddDays(30), token.ExpiresAt);
        Assert.Equal(new MeResponse(user.Id, "ash@example.com", "Ash", user.PictureUrl, EffectiveRole.User, "en", Theme.Device), session.User);
        Assert.Equal(kit.Clock.Now.AddMinutes(15), session.AccessTokenExpiresAt);
        Assert.False(string.IsNullOrEmpty(session.AccessToken));
    }

    [Fact]
    public async Task SignIn_KnownAccount_UpdatesNameEmailPictureAndStartsANewSession()
    {
        using var kit = new AuthTestKit();
        await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        kit.Clock.Advance(TimeSpan.FromDays(1));

        await kit.Sessions().SignInAsync(new GoogleIdentity("google-ash", "ash@new.example", "Ash K.", null), Ct);

        var user = await kit.Db.Users.SingleAsync(Ct);
        Assert.Equal(("ash@new.example", "Ash K.", (string?)null), (user.Email, user.DisplayName, user.PictureUrl));
        Assert.Equal(kit.Clock.Now, user.LastSignInAt);
        Assert.Equal(2, await kit.Db.RefreshTokens.Select(t => t.SessionId).Distinct().CountAsync(Ct));
    }

    [Theory]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("en", "en")]
    [InlineData(null, "en")]
    [InlineData("pt-BR", "en")]
    [InlineData("PT-pt", "en")]
    public async Task SignIn_NewAccount_StartsWithTheDevicesLanguage(string? device, string expected)
    {
        using var kit = new AuthTestKit();

        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), device, Ct);

        Assert.Equal(expected, session.User.Language);
        Assert.Equal(expected, (await kit.Db.Users.SingleAsync(Ct)).Language);
    }

    [Fact]
    public async Task SignIn_KnownAccount_KeepsItsOwnLanguage()
    {
        using var kit = new AuthTestKit();
        await kit.Sessions().SignInAsync(AuthTestKit.Identity(), "pt-PT", Ct);

        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), "en", Ct);

        Assert.Equal("pt-PT", session.User.Language);
    }

    [Fact]
    public async Task SignIn_SuperAdminEmail_GetsSuperAdminRole()
    {
        using var kit = new AuthTestKit();

        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(email: "Owner@Example.com"), Ct);

        Assert.Equal(EffectiveRole.SuperAdmin, session.User.Role);
    }

    [Fact]
    public async Task Refresh_LiveToken_SwapsItForANewOneInTheSameSession()
    {
        using var kit = new AuthTestKit();
        var first = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        kit.Clock.Advance(TimeSpan.FromMinutes(20));

        var second = await kit.Sessions().RefreshAsync(first.RefreshToken, Ct);

        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(kit.Clock.Now.AddMinutes(15), second.AccessTokenExpiresAt);
        var tokens = await kit.Db.RefreshTokens.AsNoTracking().OrderBy(t => t.CreatedAt).ToListAsync(Ct);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(kit.Clock.Now, tokens[0].UsedAt);
        Assert.Null(tokens[1].UsedAt);
        Assert.Equal(tokens[0].SessionId, tokens[1].SessionId);
        Assert.Equal(kit.Clock.Now.AddDays(30), tokens[1].ExpiresAt);
    }

    [Fact]
    public async Task Refresh_UsedTokenAgain_EndsTheWholeSession()
    {
        using var kit = new AuthTestKit();
        var first = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        var second = await kit.Sessions().RefreshAsync(first.RefreshToken, Ct);

        var reuse = await kit.Sessions().RefreshAsync(first.RefreshToken, Ct);

        Assert.Null(reuse);
        Assert.Null(await kit.Sessions(kit.NewContext()).RefreshAsync(second!.RefreshToken, Ct));
        Assert.All(await kit.NewContext().RefreshTokens.ToListAsync(Ct), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task Refresh_ReuseInOneSession_LeavesOtherSessionsAlone()
    {
        using var kit = new AuthTestKit();
        var phone = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        var laptop = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        await kit.Sessions().RefreshAsync(phone.RefreshToken, Ct);

        await kit.Sessions().RefreshAsync(phone.RefreshToken, Ct);

        Assert.NotNull(await kit.Sessions().RefreshAsync(laptop.RefreshToken, Ct));
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ReturnsNull()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        kit.Clock.Advance(TimeSpan.FromDays(30));

        Assert.Null(await kit.Sessions().RefreshAsync(session.RefreshToken, Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("junk")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Refresh_UnknownOrJunkToken_ReturnsNull(string token)
    {
        using var kit = new AuthTestKit();

        Assert.Null(await kit.Sessions().RefreshAsync(token, Ct));
    }

    [Fact]
    public async Task Refresh_RoleChangedSinceSignIn_NewTokenCarriesTheNewRole()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        var user = await kit.Db.Users.SingleAsync(Ct);
        user.Role = UserRole.Admin;
        await kit.Db.SaveChangesAsync(Ct);

        var refreshed = await kit.Sessions().RefreshAsync(session.RefreshToken, Ct);

        Assert.Equal(EffectiveRole.Admin, refreshed!.User.Role);
    }

    [Fact]
    public async Task Refresh_RacingRequestUsesTheTokenFirst_EndsTheSession()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        using var racing = kit.NewContext(new UseTokenFirst(kit));

        var result = await kit.Sessions(racing).RefreshAsync(session.RefreshToken, Ct);

        Assert.Null(result);
        Assert.All(await kit.NewContext().RefreshTokens.ToListAsync(Ct), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task Refresh_DeletesTheUsersExpiredTokens()
    {
        using var kit = new AuthTestKit();
        await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        kit.Clock.Advance(TimeSpan.FromDays(31));
        var current = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        Assert.Equal(RefreshTokenValue.Hash(current.RefreshToken), (await kit.NewContext().RefreshTokens.SingleAsync(Ct)).TokenHash);
    }

    [Fact]
    public async Task SignOut_EndsOnlyThatSession()
    {
        using var kit = new AuthTestKit();
        var phone = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        var laptop = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        await kit.Sessions().SignOutAsync(phone.RefreshToken, Ct);

        Assert.Null(await kit.Sessions().RefreshAsync(phone.RefreshToken, Ct));
        Assert.NotNull(await kit.Sessions().RefreshAsync(laptop.RefreshToken, Ct));
    }

    [Theory]
    [InlineData("junk")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task SignOut_UnknownToken_DoesNothing(string token)
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        await kit.Sessions().SignOutAsync(token, Ct);

        Assert.NotNull(await kit.Sessions().RefreshAsync(session.RefreshToken, Ct));
    }

    [Fact]
    public async Task SignOutEverywhere_EndsEverySessionOfThatUserOnly()
    {
        using var kit = new AuthTestKit();
        var phone = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        var laptop = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        var other = await kit.Sessions().SignInAsync(AuthTestKit.Identity("misty@example.com", "google-misty", "Misty"), Ct);

        await kit.Sessions().SignOutEverywhereAsync(phone.User.Id, Ct);

        Assert.Null(await kit.Sessions().RefreshAsync(phone.RefreshToken, Ct));
        Assert.Null(await kit.Sessions().RefreshAsync(laptop.RefreshToken, Ct));
        Assert.NotNull(await kit.Sessions().RefreshAsync(other.RefreshToken, Ct));
    }

    [Fact]
    public async Task SignOutEverywhere_ConflictWithARacingRefresh_IsRetried()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);
        using var racing = kit.NewContext(new UseTokenFirst(kit));

        await kit.Sessions(racing).SignOutEverywhereAsync(session.User.Id, Ct);

        Assert.All(await kit.NewContext().RefreshTokens.ToListAsync(Ct), t => Assert.NotNull(t.RevokedAt));
    }

    [Theory]
    [InlineData("owner@example.com", UserRole.User, EffectiveRole.SuperAdmin)]
    [InlineData("owner@example.com", UserRole.Admin, EffectiveRole.SuperAdmin)]
    [InlineData("misty@example.com", UserRole.Admin, EffectiveRole.Admin)]
    [InlineData("brock@example.com", UserRole.User, EffectiveRole.User)]
    public void RoleOf_SuperAdminFromConfigurationElseStoredRole(string email, UserRole stored, EffectiveRole expected) =>
        Assert.Equal(expected, Sessions.RoleOf(
            new User { GoogleSubject = "s", Email = email, DisplayName = "n", Role = stored },
            new AuthOptions { SuperAdminEmail = AuthTestKit.SuperAdminEmail }));

    /// <summary>On the first save, another request marks the token used first (a race between two tabs).</summary>
    private sealed class UseTokenFirst(AuthTestKit kit) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var other = kit.NewContext();
                foreach (var token in await other.RefreshTokens.ToListAsync(cancellationToken))
                {
                    token.UsedAt = kit.Clock.Now.AddSeconds(-1);
                }

                await other.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
