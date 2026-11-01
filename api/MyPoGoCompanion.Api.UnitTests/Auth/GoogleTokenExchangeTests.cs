using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class GoogleTokenExchangeTests
{
    private static readonly long _futureExpiry = new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static object Claims(
        string iss = "https://accounts.google.com", string aud = "client-id.apps.googleusercontent.com", long? exp = null,
        object? emailVerified = null, string? name = "Ash Ketchum", string sub = "google-123", string email = "ash@example.com") =>
        new { iss, aud, sub, exp = exp ?? _futureExpiry, email, email_verified = emailVerified ?? true, name, picture = "https://lh3.googleusercontent.com/a/x" };

    private static async Task<(GoogleIdentity? Identity, StubHandler Handler)> ExchangeAsync(HttpStatusCode status, string body)
    {
        using var kit = new AuthTestKit();
        var handler = new StubHandler(status, body);
        using var http = new HttpClient(handler);
        var exchange = new GoogleTokenExchange(http, kit.OptionsAccessor, kit.Clock, NullLogger<GoogleTokenExchange>.Instance);
        var identity = await exchange.ExchangeAsync("the-code", new string('v', 43), "https://web.example/auth/callback", TestContext.Current.CancellationToken);
        return (identity, handler);
    }

    [Fact]
    public async Task Exchange_GoodAnswer_ReturnsTheAccount()
    {
        var (identity, _) = await ExchangeAsync(HttpStatusCode.OK, StubHandler.TokenResponse(Claims()));

        Assert.Equal(new GoogleIdentity("google-123", "ash@example.com", "Ash Ketchum", "https://lh3.googleusercontent.com/a/x"), identity);
    }

    [Fact]
    public async Task Exchange_SendsCodeVerifierSecretAndRedirectToGoogle()
    {
        var (_, handler) = await ExchangeAsync(HttpStatusCode.OK, StubHandler.TokenResponse(Claims()));

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(GoogleTokenExchange.TokenEndpoint, handler.Request.RequestUri);
        var form = handler.RequestBody!.Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("the-code", form["code"]);
        Assert.Equal(new string('v', 43), form["code_verifier"]);
        Assert.Equal("https://web.example/auth/callback", form["redirect_uri"]);
        Assert.Equal("client-id.apps.googleusercontent.com", form["client_id"]);
        Assert.Equal("client-secret", form["client_secret"]);
    }

    [Fact]
    public async Task Exchange_ShortIssuerAndStringVerified_AreAccepted()
    {
        var (identity, _) = await ExchangeAsync(HttpStatusCode.OK, StubHandler.TokenResponse(Claims(iss: "accounts.google.com", emailVerified: "true")));

        Assert.NotNull(identity);
    }

    [Fact]
    public async Task Exchange_NoName_UsesTheEmail()
    {
        var (identity, _) = await ExchangeAsync(HttpStatusCode.OK, StubHandler.TokenResponse(Claims(name: null)));

        Assert.Equal("ash@example.com", identity!.Name);
    }

    public static TheoryData<string, object> BadClaims => new()
    {
        { "other issuer", Claims(iss: "https://evil.example") },
        { "other audience", Claims(aud: "someone-elses-client") },
        { "expired", Claims(exp: new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()) },
        { "email not verified", Claims(emailVerified: false) },
        { "email verified as text false", Claims(emailVerified: "false") },
        { "email verified as a number", Claims(emailVerified: 1) },
        { "no subject", Claims(sub: "") },
        { "no email", Claims(email: " ") },
    };

    [Theory]
    [MemberData(nameof(BadClaims))]
    public async Task Exchange_ClaimsFailACheck_ReturnsNull(string why, object claims)
    {
        var (identity, _) = await ExchangeAsync(HttpStatusCode.OK, StubHandler.TokenResponse(claims));

        Assert.True(identity is null, why);
    }

    [Theory]
    [InlineData("""{"error":"invalid_grant"}""")]
    [InlineData("""{"error":"redirect_uri_mismatch","error_description":"long ................................................................................................................................................................................................................................................................................................................"}""")]
    public async Task Exchange_GoogleRefuses_ReturnsNull(string body)
    {
        var (identity, _) = await ExchangeAsync(HttpStatusCode.BadRequest, body);

        Assert.Null(identity);
    }

    [Theory]
    [InlineData("""{"access_token":"x"}""")]
    [InlineData("""{"id_token":"not-a-jwt"}""")]
    [InlineData("""{"id_token":"a.%%%.c"}""")]
    [InlineData("""{"id_token":"a.bm90IGpzb24.c"}""")]
    public async Task Exchange_NoUsableIdToken_ReturnsNull(string body)
    {
        var (identity, _) = await ExchangeAsync(HttpStatusCode.OK, body);

        Assert.Null(identity);
    }

    private static readonly System.Text.Json.JsonSerializerOptions _withConverter = new()
    {
        Converters = { new GoogleTokenExchange.BoolOrStringConverter() },
    };

    [Fact]
    public void BoolOrStringConverter_WritesABoolean() =>
        Assert.Equal("true", System.Text.Json.JsonSerializer.Serialize(true, _withConverter));
}
