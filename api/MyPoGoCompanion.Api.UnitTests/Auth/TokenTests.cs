using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class TokenTests
{
    [Fact]
    public async Task AccessToken_ValidatesWithOurParametersAndCarriesTheUser()
    {
        using var kit = new AuthTestKit();
        var userId = Guid.NewGuid();

        var (token, expiresAt) = kit.AccessTokens.Create(userId, "ash@example.com", "Ash", EffectiveRole.Admin);

        Assert.Equal(kit.Clock.Now.AddMinutes(15), expiresAt);
        var parameters = AccessTokens.ValidationParameters(kit.Key.Value);
        parameters.LifetimeValidator = (_, _, _, _) => true; // the test clock is not the machine's clock
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid, result.Exception?.Message);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.Equal(userId, AccessTokens.UserId(principal));
        Assert.Equal("Ash", principal.Identity!.Name);
        Assert.True(principal.IsInRole("Admin"));
        Assert.Equal("ash@example.com", principal.FindFirstValue("email"));
    }

    [Fact]
    public async Task AccessToken_OtherKey_IsRejected()
    {
        using var kit = new AuthTestKit();
        using var other = new AuthTestKit(o => o.SigningKey = Convert.ToBase64String(new byte[32]));
        var (token, _) = kit.AccessTokens.Create(Guid.NewGuid(), "a@example.com", "A", EffectiveRole.User);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, AccessTokens.ValidationParameters(other.Key.Value));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UserId_NoOrBadSubject_IsNull()
    {
        Assert.Null(AccessTokens.UserId(new ClaimsPrincipal()));
        Assert.Null(AccessTokens.UserId(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "not-a-guid")]))));
    }

    [Fact]
    public void SigningKey_NotConfigured_IsRandom32Bytes()
    {
        var first = SigningKey.From(new AuthOptions());
        var second = SigningKey.From(new AuthOptions());

        Assert.Equal(32, first.Value.Key.Length);
        Assert.NotEqual(first.Value.Key, second.Value.Key);
    }

    [Fact]
    public void RefreshToken_Is43Base64UrlCharactersAndHashesToHex()
    {
        var token = RefreshTokenValue.New();

        Assert.True(RefreshTokenValue.LooksValid(token));
        Assert.NotEqual(token, RefreshTokenValue.New());
        Assert.Matches("^[0-9a-f]{64}$", RefreshTokenValue.Hash(token));
        Assert.Equal(RefreshTokenValue.Hash(token), RefreshTokenValue.Hash(token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("0123456789012345678901234567890123456789+/=")]
    public void RefreshToken_LooksValid_RejectsJunk(string? token) => Assert.False(RefreshTokenValue.LooksValid(token));
}
