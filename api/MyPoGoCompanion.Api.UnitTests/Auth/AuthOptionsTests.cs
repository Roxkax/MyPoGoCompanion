using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class AuthOptionsTests
{
    private static AuthOptions Complete() => new()
    {
        GoogleClientId = "id",
        GoogleClientSecret = "secret",
        SigningKey = AuthTestKit.SigningKeyText,
    };

    [Fact]
    public void IsConfigured_AllThreeSecrets_IsTrue() => Assert.True(Complete().IsConfigured);

    [Theory]
    [InlineData("client")]
    [InlineData("secret")]
    [InlineData("key")]
    public void IsConfigured_OneMissing_IsFalse(string missing)
    {
        var options = Complete();
        switch (missing)
        {
            case "client": options.GoogleClientId = " "; break;
            case "secret": options.GoogleClientSecret = null; break;
            default: options.SigningKey = ""; break;
        }

        Assert.False(options.IsConfigured);
    }

    [Theory]
    [InlineData("not base64 at all!")]
    [InlineData("c2hvcnQ=")] // "short": under 32 bytes
    public void SigningKeyBytes_InvalidOrShort_IsNull(string key) =>
        Assert.Null(new AuthOptions { SigningKey = key }.SigningKeyBytes());

    [Fact]
    public void SigningKeyBytes_Valid_ReturnsTheBytes() =>
        Assert.Equal(Enumerable.Range(1, 32).Select(i => (byte)i), Complete().SigningKeyBytes()!);

    [Theory]
    [InlineData("owner@example.com", true)]
    [InlineData(" OWNER@Example.com ", true)]
    [InlineData("someone@example.com", false)]
    [InlineData(null, false)]
    public void IsSuperAdmin_ComparesEmailsIgnoringCase(string? email, bool expected) =>
        Assert.Equal(expected, new AuthOptions { SuperAdminEmail = "owner@example.com" }.IsSuperAdmin(email));

    [Fact]
    public void IsSuperAdmin_NotConfigured_NobodyIs() =>
        Assert.False(new AuthOptions().IsSuperAdmin("owner@example.com"));

    [Fact]
    public void Lifetimes_Are15MinutesAnd30Days()
    {
        var options = new AuthOptions();
        Assert.Equal(TimeSpan.FromMinutes(15), options.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(30), options.RefreshTokenLifetime);
    }
}
