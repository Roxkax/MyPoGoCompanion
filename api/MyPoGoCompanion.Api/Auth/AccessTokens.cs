using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace MyPoGoCompanion.Api.Auth;

/// <summary>What a user may do: the stored role, or super admin when their email is the configured one.</summary>
public enum EffectiveRole
{
    User,
    Admin,
    SuperAdmin,
}

/// <summary>
/// Our own short-lived access tokens (signed JWT, HMAC-SHA256). They carry who the user is and their role for the
/// web app's display; the API itself re-reads the role from the database on admin requests.
/// </summary>
public sealed class AccessTokens(IOptions<AuthOptions> options, SigningKey key, TimeProvider time)
{
    public const string RoleClaim = "role";

    public (string Token, DateTimeOffset ExpiresAt) Create(Guid userId, string email, string name, EffectiveRole role)
    {
        var now = time.GetUtcNow();
        var expires = now + options.Value.AccessTokenLifetime;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AuthOptions.Issuer,
            Audience = AuthOptions.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(key.Value, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Name, name),
                new Claim(RoleClaim, role.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
        });
        return (token, expires);
    }

    /// <summary>How the API checks an access token: our issuer, audience and key, not expired (30 s of clock slack).</summary>
    public static TokenValidationParameters ValidationParameters(SecurityKey key) => new()
    {
        ValidIssuer = AuthOptions.Issuer,
        ValidAudience = AuthOptions.Audience,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = JwtRegisteredClaimNames.Name,
        RoleClaimType = RoleClaim,
    };

    /// <summary>The user id in a validated access token, or null.</summary>
    public static Guid? UserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}

/// <summary>
/// The key that signs access tokens. Without a configured key (sign-in not set up) a random one is used, so the API
/// still starts; no tokens are issued then anyway.
/// </summary>
public sealed class SigningKey(SymmetricSecurityKey value)
{
    public SymmetricSecurityKey Value { get; } = value;

    public static SigningKey From(AuthOptions options) =>
        new(new SymmetricSecurityKey(options.SigningKeyBytes() ?? System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
}
