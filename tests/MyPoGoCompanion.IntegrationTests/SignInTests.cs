using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>Signing in, refresh-token rotation, signing out and roles, through HTTP against a real database.</summary>
public sealed class SignInTests(PostgresFixture postgres)
{
    private const string Callback = ApiFactory.WebOrigin + "/auth/callback";
    private static readonly string _verifier = new('v', 64);
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FakeGoogle Google() => new FakeGoogle()
        .Add("owner-code", ApiFactory.SuperAdminEmail, "Diogo")
        .Add("misty-code", "misty@example.test", "Misty")
        .Add("brock-code", "brock@example.test", "Brock");

    private static async Task<SessionResponse> SignInAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/auth/google", new { code, codeVerifier = _verifier, redirectUri = Callback }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(_json, Ct))!;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/auth/refresh", new { refreshToken = token }, Ct);

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
        {
            Content = body is null ? null : JsonContent.Create(body, options: _json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task SignIn_ThenMe_ThenRefresh_ThenReuse_EndsTheSignIn()
    {
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct), Google());
        using var client = api.CreateClient();

        var session = await SignInAsync(client, "misty-code");
        Assert.Equal(("Misty", EffectiveRole.User), (session.User.Name, session.User.Role));

        using (var me = Authorized(HttpMethod.Get, "/me", session.AccessToken))
        {
            var response = await client.SendAsync(me, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("misty@example.test", (await response.Content.ReadFromJsonAsync<MeResponse>(_json, Ct))!.Email);
        }

        var refreshed = await RefreshAsync(client, session.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var next = (await refreshed.Content.ReadFromJsonAsync<SessionResponse>(_json, Ct))!;
        Assert.NotEqual(session.RefreshToken, next.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, session.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, next.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Me_WithoutOrWithAForgedToken_Returns401()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, Google());
        using var client = api.CreateClient();
        using var other = new AuthTestKitForgery();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/me", UriKind.Relative), Ct)).StatusCode);
        using var forged = Authorized(HttpMethod.Get, "/me", other.Token());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(forged, Ct)).StatusCode);
    }

    [Fact]
    public async Task SignOut_EndsThatDevice_SignOutEverywhere_EndsTheRest()
    {
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct), Google());
        using var client = api.CreateClient();
        var phone = await SignInAsync(client, "misty-code");
        var laptop = await SignInAsync(client, "misty-code");
        var tablet = await SignInAsync(client, "misty-code");

        var signOut = await client.PostAsJsonAsync("/auth/sign-out", new { refreshToken = phone.RefreshToken }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, phone.RefreshToken)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(new Uri("/auth/sign-out-everywhere", UriKind.Relative), null, Ct)).StatusCode);
        using var everywhere = Authorized(HttpMethod.Post, "/auth/sign-out-everywhere", laptop.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(everywhere, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, laptop.RefreshToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(client, tablet.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task SignIn_StoresOnlyTheTokenHash()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var api = new ApiFactory(database, Google());
        using var client = api.CreateClient();

        var session = await SignInAsync(client, "misty-code");

        await using var db = PostgresFixture.CreateContext(database);
        var stored = await db.RefreshTokens.SingleAsync(Ct);
        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
    }

    [Fact]
    public async Task SignIn_UnknownCodeOrOtherReturnAddress_Returns400()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, Google());
        using var client = api.CreateClient();

        var unknown = await client.PostAsJsonAsync("/auth/google", new { code = "nope", codeVerifier = _verifier, redirectUri = Callback }, Ct);
        var elsewhere = await client.PostAsJsonAsync("/auth/google",
            new { code = "misty-code", codeVerifier = _verifier, redirectUri = "https://evil.example/auth/callback" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, elsewhere.StatusCode);
    }

    [Fact]
    public async Task SignInNotConfigured_ClientAndSignInReturn503_RestOfTheApiWorks()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, Google(), signInConfigured: false);
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(new Uri("/auth/google/client", UriKind.Relative), Ct)).StatusCode);
        var signIn = await client.PostAsJsonAsync("/auth/google", new { code = "misty-code", codeVerifier = _verifier, redirectUri = Callback }, Ct);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, signIn.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task GoogleClient_Configured_ReturnsTheClientId()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, Google());
        using var client = api.CreateClient();

        var body = await client.GetFromJsonAsync<GoogleClientResponse>("/auth/google/client", _json, Ct);

        Assert.Equal("test-client.apps.googleusercontent.com", body!.ClientId);
    }

    [Fact]
    public async Task UsersAndRoles_OnlyTheSuperAdmin_AndAChangeIsKept()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var api = new ApiFactory(database, Google());
        using var client = api.CreateClient();
        var owner = await SignInAsync(client, "owner-code");
        var misty = await SignInAsync(client, "misty-code");
        await SignInAsync(client, "brock-code");
        Assert.Equal(EffectiveRole.SuperAdmin, owner.User.Role);

        using (var asUser = Authorized(HttpMethod.Get, "/admin/users", misty.AccessToken))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(asUser, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/admin/users", UriKind.Relative), Ct)).StatusCode);

        using (var makeAdmin = Authorized(HttpMethod.Put, $"/admin/users/{misty.User.Id}/role", owner.AccessToken, new { role = "Admin" }))
        {
            var response = await client.SendAsync(makeAdmin, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(EffectiveRole.Admin, (await response.Content.ReadFromJsonAsync<AdminUserResponse>(_json, Ct))!.Role);
        }

        // An admin still can't hand out roles.
        using (var asAdmin = Authorized(HttpMethod.Put, $"/admin/users/{misty.User.Id}/role", misty.AccessToken, new { role = "User" }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(asAdmin, Ct)).StatusCode);
        }

        using (var list = Authorized(HttpMethod.Get, "/admin/users", owner.AccessToken))
        {
            var users = await (await client.SendAsync(list, Ct)).Content.ReadFromJsonAsync<AdminUserResponse[]>(_json, Ct);
            Assert.Equal([("Brock", EffectiveRole.User), ("Diogo", EffectiveRole.SuperAdmin), ("Misty", EffectiveRole.Admin)],
                users!.Select(u => (u.Name, u.Role)));
        }

        // The refreshed access token carries the new role for the web app.
        var refreshed = await (await RefreshAsync(client, misty.RefreshToken)).Content.ReadFromJsonAsync<SessionResponse>(_json, Ct);
        Assert.Equal(EffectiveRole.Admin, refreshed!.User.Role);

        await using var db = PostgresFixture.CreateContext(database);
        var change = await db.RoleChanges.SingleAsync(Ct);
        Assert.Equal((misty.User.Id, owner.User.Id, UserRole.User, UserRole.Admin), (change.UserId, change.ChangedByUserId!.Value, change.From, change.To));
    }

    [Theory]
    [InlineData("mis", new[] { "Misty" })]
    [InlineData("EXAMPLE.TEST", new[] { "Brock", "Diogo", "Misty" })]
    [InlineData("%", new string[0])]
    [InlineData("_", new string[0])]
    public async Task ListUsers_SearchMatchesNameOrEmail_WildcardsAreLiteral(string search, string[] expected)
    {
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct), Google());
        using var client = api.CreateClient();
        var owner = await SignInAsync(client, "owner-code");
        await SignInAsync(client, "misty-code");
        await SignInAsync(client, "brock-code");

        using var list = Authorized(HttpMethod.Get, $"/admin/users?search={Uri.EscapeDataString(search)}", owner.AccessToken);
        var users = await (await client.SendAsync(list, Ct)).Content.ReadFromJsonAsync<AdminUserResponse[]>(_json, Ct);

        Assert.Equal(expected, users!.Select(u => u.Name));
    }

    [Fact]
    public async Task RateLimit_PerClientAddress_On_Auth()
    {
        await using var api = new ApiFactory(postgres.ConnectionString, Google());
        using var client = api.CreateClient();

        async Task<HttpStatusCode> CallAsync(string address)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/auth/google/client", UriKind.Relative));
            request.Headers.Add("X-Forwarded-For", address);
            return (await client.SendAsync(request, Ct)).StatusCode;
        }

        for (var i = 0; i < 20; i++) // SignInSetup.RequestsPerMinute
        {
            Assert.Equal(HttpStatusCode.OK, await CallAsync("198.51.100.1"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await CallAsync("198.51.100.1"));
        Assert.Equal(HttpStatusCode.OK, await CallAsync("198.51.100.2"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task DeletingAUser_TakesTheirTokensAndRoleHistory_KeepsRolesTheyGranted()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var api = new ApiFactory(database, Google());
        using var client = api.CreateClient();
        var owner = await SignInAsync(client, "owner-code");
        var misty = await SignInAsync(client, "misty-code");
        using (var makeAdmin = Authorized(HttpMethod.Put, $"/admin/users/{misty.User.Id}/role", owner.AccessToken, new { role = "Admin" }))
        {
            await client.SendAsync(makeAdmin, Ct);
        }

        await using (var db = PostgresFixture.CreateContext(database))
        {
            await db.Users.Where(u => u.Id == owner.User.Id).ExecuteDeleteAsync(Ct);
        }

        await using (var db = PostgresFixture.CreateContext(database))
        {
            var mistyNow = await db.Users.SingleAsync(Ct);
            Assert.Equal((UserRole.Admin, (Guid?)null), (mistyNow.Role, mistyNow.RoleGrantedByUserId));
            Assert.Null((await db.RoleChanges.SingleAsync(Ct)).ChangedByUserId);
            Assert.All(await db.RefreshTokens.ToListAsync(Ct), t => Assert.Equal(misty.User.Id, t.UserId));

            await db.Users.ExecuteDeleteAsync(Ct);
            Assert.Empty(db.RefreshTokens);
            Assert.Empty(db.RoleChanges);
        }
    }

    [Fact]
    public async Task RefreshTokens_RejectAHashThatIsNotSha256Hex()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        await using var db = PostgresFixture.CreateContext(database);
        var user = new User { GoogleSubject = "s", Email = "e@example.test", DisplayName = "E" };
        db.Users.Add(user);
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, SessionId = Guid.NewGuid(), TokenHash = "plain-token", CreatedAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));

        Assert.Contains("ck_refresh_tokens_hash", error.InnerException!.Message, StringComparison.Ordinal);
    }

    /// <summary>An access token signed with a key the API doesn't know.</summary>
    private sealed class AuthTestKitForgery : IDisposable
    {
        public string Token()
        {
            var options = new AuthOptions { SigningKey = Convert.ToBase64String(new byte[32]) };
            var tokens = new AccessTokens(Microsoft.Extensions.Options.Options.Create(options), Api.Auth.SigningKey.From(options), TimeProvider.System);
            return tokens.Create(Guid.NewGuid(), "x@example.test", "X", EffectiveRole.SuperAdmin).Token;
        }

        public void Dispose()
        {
        }
    }
}
