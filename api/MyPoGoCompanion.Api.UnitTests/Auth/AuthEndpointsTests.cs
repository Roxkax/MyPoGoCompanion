using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class AuthEndpointsTests
{
    private const string Callback = "https://web.example/auth/callback";
    private static readonly string _verifier = new('a', 43);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection([new("Cors:AllowedOrigins", "https://web.example, http://localhost:5173/")])
        .Build();

    private static ClaimsPrincipal Signed(Guid id) => new(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));

    private sealed class FakeGoogle(GoogleIdentity? answer) : IGoogleTokenExchange
    {
        public int Calls { get; private set; }

        public Task<GoogleIdentity?> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(answer);
        }
    }

    [Fact]
    public void GetGoogleClient_Configured_ReturnsTheClientId()
    {
        using var kit = new AuthTestKit();

        var ok = Assert.IsType<Ok<GoogleClientResponse>>(AuthEndpoints.GetGoogleClient(kit.OptionsAccessor).Result);

        Assert.Equal("client-id.apps.googleusercontent.com", ok.Value!.ClientId);
    }

    [Fact]
    public void GetGoogleClient_NotConfigured_Returns503()
    {
        using var kit = new AuthTestKit(o => o.GoogleClientSecret = null);

        var problem = Assert.IsType<ProblemHttpResult>(AuthEndpoints.GetGoogleClient(kit.OptionsAccessor).Result);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
    }

    [Fact]
    public async Task SignIn_GoodRequest_ReturnsTokens()
    {
        using var kit = new AuthTestKit();

        var result = await AuthEndpoints.SignInWithGoogleAsync(
            new GoogleSignInRequest("code", _verifier, Callback), kit.OptionsAccessor, Config(),
            new FakeGoogle(AuthTestKit.Identity()), kit.Sessions(), Ct);

        Assert.Equal("ash@example.com", Assert.IsType<Ok<SessionResponse>>(result.Result).Value!.User.Email);
    }

    [Fact]
    public async Task SignIn_LocalhostOriginWithTrailingSlash_IsAllowed()
    {
        using var kit = new AuthTestKit();

        var result = await AuthEndpoints.SignInWithGoogleAsync(
            new GoogleSignInRequest("code", _verifier, "http://localhost:5173/auth/callback"), kit.OptionsAccessor, Config(),
            new FakeGoogle(AuthTestKit.Identity()), kit.Sessions(), Ct);

        Assert.IsType<Ok<SessionResponse>>(result.Result);
    }

    public static TheoryData<string, string?, string?, string?> BadRequests => new()
    {
        { "no code", null, _verifier, Callback },
        { "code with spaces", "a b", _verifier, Callback },
        { "huge code", new string('c', 513), _verifier, Callback },
        { "short verifier", "code", new string('a', 42), Callback },
        { "long verifier", "code", new string('a', 129), Callback },
        { "verifier with bad characters", "code", new string('a', 42) + "+", Callback },
        { "no redirect", "code", _verifier, null },
        { "other site", "code", _verifier, "https://evil.example/auth/callback" },
        { "other path", "code", _verifier, "https://web.example/somewhere" },
        { "prefix trick", "code", _verifier, "https://web.example.evil.example/auth/callback" },
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task SignIn_BadRequest_Returns400WithoutAskingGoogle(string why, string? code, string? verifier, string? redirect)
    {
        using var kit = new AuthTestKit();
        var google = new FakeGoogle(AuthTestKit.Identity());

        var result = await AuthEndpoints.SignInWithGoogleAsync(
            new GoogleSignInRequest(code, verifier, redirect), kit.OptionsAccessor, Config(), google, kit.Sessions(), Ct);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        Assert.True(google.Calls == 0, why);
    }

    [Fact]
    public async Task SignIn_GoogleRefuses_Returns400()
    {
        using var kit = new AuthTestKit();

        var result = await AuthEndpoints.SignInWithGoogleAsync(
            new GoogleSignInRequest("code", _verifier, Callback), kit.OptionsAccessor, Config(), new FakeGoogle(null), kit.Sessions(), Ct);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        Assert.Empty(kit.Db.Users);
    }

    [Fact]
    public async Task SignIn_NotConfigured_Returns503()
    {
        using var kit = new AuthTestKit(o => o.SigningKey = null);

        var result = await AuthEndpoints.SignInWithGoogleAsync(
            new GoogleSignInRequest("code", _verifier, Callback), kit.OptionsAccessor, Config(),
            new FakeGoogle(AuthTestKit.Identity()), kit.Sessions(), Ct);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Fact]
    public async Task Refresh_GoodToken_Returns200_BadToken_Returns401()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        Assert.IsType<Ok<SessionResponse>>((await AuthEndpoints.RefreshAsync(new RefreshRequest(session.RefreshToken), kit.Sessions(), Ct)).Result);
        Assert.IsType<UnauthorizedHttpResult>((await AuthEndpoints.RefreshAsync(new RefreshRequest(null), kit.Sessions(), Ct)).Result);
    }

    [Fact]
    public async Task SignOut_Returns204AndEndsTheSession()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        Assert.IsType<NoContent>(await AuthEndpoints.SignOutAsync(new RefreshRequest(session.RefreshToken), kit.Sessions(), Ct));
        Assert.IsType<NoContent>(await AuthEndpoints.SignOutAsync(new RefreshRequest(null), kit.Sessions(), Ct));

        Assert.Null(await kit.Sessions().RefreshAsync(session.RefreshToken, Ct));
    }

    [Fact]
    public async Task SignOutEverywhere_SignedIn_Returns204_NoUserId_Returns401()
    {
        using var kit = new AuthTestKit();
        var session = await kit.Sessions().SignInAsync(AuthTestKit.Identity(), Ct);

        Assert.IsType<NoContent>((await AuthEndpoints.SignOutEverywhereAsync(Signed(session.User.Id), kit.Sessions(), Ct)).Result);
        Assert.IsType<UnauthorizedHttpResult>((await AuthEndpoints.SignOutEverywhereAsync(new ClaimsPrincipal(), kit.Sessions(), Ct)).Result);
        Assert.Null(await kit.Sessions().RefreshAsync(session.RefreshToken, Ct));
    }

    [Fact]
    public async Task GetMe_KnownUser_ReturnsThem_DeletedOrMissing_Returns401()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail, name: "Owner");

        var ok = Assert.IsType<Ok<MeResponse>>((await AuthEndpoints.GetMeAsync(Signed(owner.Id), kit.Db, kit.OptionsAccessor, Ct)).Result);
        Assert.Equal(("Owner", EffectiveRole.SuperAdmin), (ok.Value!.Name, ok.Value.Role));

        Assert.IsType<UnauthorizedHttpResult>((await AuthEndpoints.GetMeAsync(Signed(Guid.NewGuid()), kit.Db, kit.OptionsAccessor, Ct)).Result);
        Assert.IsType<UnauthorizedHttpResult>((await AuthEndpoints.GetMeAsync(new ClaimsPrincipal(), kit.Db, kit.OptionsAccessor, Ct)).Result);
    }

    [Theory]
    [InlineData("abc-._~ABC0123456789abcdefghijklmnopqrstuvwx", true)]
    [InlineData("abc", false)]
    public void IsValidCodeVerifier_FollowsRfc7636(string verifier, bool expected) =>
        Assert.Equal(expected, AuthEndpoints.IsValidCodeVerifier(verifier));
}
