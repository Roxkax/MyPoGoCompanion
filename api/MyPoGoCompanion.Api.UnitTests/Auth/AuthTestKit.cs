using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

/// <summary>A clock the test moves by hand.</summary>
internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>Everything the sign-in code needs, on an in-memory database of its own.</summary>
internal sealed class AuthTestKit : IDisposable
{
    public const string SuperAdminEmail = "owner@example.com";
    public static readonly string SigningKeyText = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());

    private readonly string _database = Guid.NewGuid().ToString();

    public AuthTestKit(Action<AuthOptions>? configure = null)
    {
        Options = new AuthOptions
        {
            GoogleClientId = "client-id.apps.googleusercontent.com",
            GoogleClientSecret = "client-secret",
            SigningKey = SigningKeyText,
            SuperAdminEmail = SuperAdminEmail,
        };
        configure?.Invoke(Options);
        Key = SigningKey.From(Options);
        AccessTokens = new AccessTokens(Microsoft.Extensions.Options.Options.Create(Options), Key, Clock);
        Db = NewContext();
    }

    public TestClock Clock { get; } = new(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

    public AuthOptions Options { get; }

    public IOptions<AuthOptions> OptionsAccessor => Microsoft.Extensions.Options.Options.Create(Options);

    public SigningKey Key { get; }

    public AccessTokens AccessTokens { get; }

    public AppDbContext Db { get; }

    public AppDbContext NewContext(params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_database).AddInterceptors(interceptors).Options);

    public Sessions Sessions(AppDbContext? db = null) =>
        new(db ?? Db, AccessTokens, OptionsAccessor, Clock, NullLogger<Sessions>.Instance);

    public async Task<User> AddUserAsync(string email, UserRole role = UserRole.User, string name = "Someone")
    {
        var user = new User { GoogleSubject = "sub-" + email, Email = email, DisplayName = name, Role = role, CreatedAt = Clock.Now, LastSignInAt = Clock.Now };
        Db.Users.Add(user);
        await Db.SaveChangesAsync();
        return user;
    }

    public static GoogleIdentity Identity(string email = "ash@example.com", string subject = "google-ash", string name = "Ash") =>
        new(subject, email, name, "https://lh3.googleusercontent.com/a/ash");

    public void Dispose() => Db.Dispose();
}

/// <summary>An HTTP handler that answers every request with a fixed response and remembers the request.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }

    public string? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    /// <summary>Google's token response holding an (unsigned) ID token with these claims.</summary>
    public static string TokenResponse(object claims) =>
        JsonSerializer.Serialize(new { access_token = "google-access", id_token = IdToken(claims), token_type = "Bearer" });

    public static string IdToken(object claims) =>
        $"{Encode(new { alg = "RS256", typ = "JWT" })}.{Encode(claims)}.c2lnbmF0dXJl";

    private static string Encode(object value) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
