using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// The OpenAPI document is the contract the generated clients are built from: the API serves exactly the committed
/// <c>api/openapi.json</c>, and what it really answers (status, content type, required fields) is what the document
/// says, for every operation.
/// </summary>
public sealed partial class OpenApiTests(PostgresFixture postgres)
{
    private const string Callback = ApiFactory.WebOrigin + "/auth/callback";
    private static readonly string _verifier = new('v', 64);
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string CommittedDocumentPath
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MyPoGoCompanion.slnx")))
                {
                    return Path.Combine(directory.FullName, "api", "openapi.json");
                }
            }

            throw new InvalidOperationException("The repository root (MyPoGoCompanion.slnx) wasn't found.");
        }
    }

    private static JsonObject Committed => JsonNode.Parse(File.ReadAllText(CommittedDocumentPath))!.AsObject();

    [Fact]
    public async Task ServedDocument_IsTheCommittedOne()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();

        var served = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct);

        // A difference means api/openapi.json is out of date: build the API in Debug (docs/API.md) and commit it.
        Assert.Equal(Normalize(await File.ReadAllTextAsync(CommittedDocumentPath, Ct)), Normalize(served));
    }

    [Fact]
    public void Document_EveryRequestAndResponseObjectHasAnExample()
    {
        var schemas = Committed["components"]!["schemas"]!.AsObject();

        var withoutExample = schemas
            .Where(schema => schema.Value!["type"]?.ToString() == "object" && schema.Value["examples"] is null)
            .Select(schema => schema.Key);

        Assert.Empty(withoutExample);
    }

    [Fact]
    public void Document_SignedInOperationsAskForTheTokenAndDocument401()
    {
        foreach (var (path, method, operation) in Operations(Committed))
        {
            var signedIn = operation["security"] is not null;
            var responses = operation["responses"]!.AsObject();
            if (signedIn)
            {
                Assert.True(responses.ContainsKey("401"), $"{method} {path} needs a token but doesn't document 401");
            }
            else
            {
                Assert.False(responses.ContainsKey("403"), $"{method} {path} is open to everyone but documents 403");
            }
        }

        var me = Committed["paths"]!["/me"]!["get"]!;
        Assert.Equal("Bearer", me["security"]![0]!.AsObject().Single().Key);
        Assert.Equal("http", Committed["components"]!["securitySchemes"]!["Bearer"]!["type"]!.ToString());
        Assert.Null(Committed["paths"]!["/health"]!["get"]!["security"]);
    }

    [Fact]
    public void Document_HasNoServerAddress_AndHidesTheLivenessProbe()
    {
        // The same document everywhere (local, QA, prod): clients set the API's address themselves.
        Assert.Null(Committed["servers"]);
        Assert.False(Committed["paths"]!.AsObject().ContainsKey("/health/live"));
    }

    [Fact]
    public async Task RealResponses_MatchTheDocument_ForEveryOperation()
    {
        var database = await postgres.CreateDatabaseAsync(Ct);
        var google = new FakeGoogle()
            .Add("owner-code", ApiFactory.SuperAdminEmail, "Diogo")
            .Add("misty-code", "misty@example.test", "Misty");
        await using var api = new ApiFactory(database, google);
        await using var notConfigured = new ApiFactory(database, signInConfigured: false);
        using var client = api.CreateClient();
        using var withoutSignIn = notConfigured.CreateClient();
        var document = Committed;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        async Task<HttpResponseMessage> Call(HttpClient http, HttpMethod method, string path, object? body = null, string? token = null)
        {
            using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative))
            {
                Content = body is null ? null : JsonContent.Create(body, options: _json),
            };
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await http.SendAsync(request, Ct);
            seen.Add(await AssertMatchesDocumentAsync(document, method, path, response));
            return response;
        }

        // Sign-in
        await Call(client, HttpMethod.Get, "/auth/google/client");
        await Call(withoutSignIn, HttpMethod.Get, "/auth/google/client");
        await Call(withoutSignIn, HttpMethod.Post, "/auth/google", new { code = "owner-code", codeVerifier = _verifier, redirectUri = Callback });
        await Call(client, HttpMethod.Post, "/auth/google", new { code = "unknown", codeVerifier = _verifier, redirectUri = Callback });
        var owner = await ReadSessionAsync(await Call(client, HttpMethod.Post, "/auth/google",
            new { code = "owner-code", codeVerifier = _verifier, redirectUri = Callback, language = "pt-PT" }));
        var misty = await ReadSessionAsync(await Call(client, HttpMethod.Post, "/auth/google",
            new { code = "misty-code", codeVerifier = _verifier, redirectUri = Callback }));
        await Call(client, HttpMethod.Post, "/auth/refresh", new { refreshToken = "not-a-token" });
        misty = await ReadSessionAsync(await Call(client, HttpMethod.Post, "/auth/refresh", new { refreshToken = misty.RefreshToken }));

        // Account
        await Call(client, HttpMethod.Get, "/me");
        await Call(client, HttpMethod.Get, "/me", token: misty.AccessToken);
        await Call(client, HttpMethod.Put, "/me/language", new { language = "pt-PT" });
        await Call(client, HttpMethod.Put, "/me/language", new { language = "fr" }, misty.AccessToken);
        await Call(client, HttpMethod.Put, "/me/language", new { language = "pt-PT" }, misty.AccessToken);

        // Admin
        await Call(client, HttpMethod.Get, "/admin/users");
        await Call(client, HttpMethod.Get, "/admin/users", token: misty.AccessToken);
        await Call(client, HttpMethod.Get, "/admin/users?search=mis&limit=5", token: owner.AccessToken);
        await Call(client, HttpMethod.Put, $"/admin/users/{Guid.NewGuid()}/role", new { role = "Admin" }, owner.AccessToken);
        await Call(client, HttpMethod.Put, $"/admin/users/{misty.User.Id}/role", new { role = "Nope" }, owner.AccessToken);
        await Call(client, HttpMethod.Put, $"/admin/users/{misty.User.Id}/role", new { role = "Admin" }, owner.AccessToken);

        // Health, then signing out
        await Call(client, HttpMethod.Get, "/health");
        await Call(client, HttpMethod.Post, "/auth/sign-out-everywhere");
        await Call(client, HttpMethod.Post, "/auth/sign-out-everywhere", token: owner.AccessToken);
        await Call(client, HttpMethod.Post, "/auth/sign-out", new { refreshToken = misty.RefreshToken });

        // A new endpoint needs a line here, so its real answers are checked against the document too.
        var operations = Operations(document).Select(o => $"{o.Method} {o.Path}").ToHashSet(StringComparer.Ordinal);
        Assert.Empty(operations.Except(seen.Select(s => s[..s.LastIndexOf(' ')])));
    }

    [Fact]
    public async Task TooManySignInRequests_Return429AsTheDocumentSays()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        HttpResponseMessage response;
        do
        {
            response = await client.GetAsync(new Uri("/auth/google/client", UriKind.Relative), Ct);
        }
        while (response.StatusCode != HttpStatusCode.TooManyRequests);

        await AssertMatchesDocumentAsync(Committed, HttpMethod.Get, "/auth/google/client", response);
    }

    [Fact]
    public async Task DocsPage_OnlyWhereSwitchedOn()
    {
        await using var on = new ApiFactory(postgres.ConnectionString, settings: new Dictionary<string, string?> { ["ApiDocs:Enabled"] = "true" });
        await using var off = new ApiFactory(postgres.ConnectionString);
        using var onClient = on.CreateClient();
        using var offClient = off.CreateClient();

        var page = await onClient.GetAsync(new Uri("/docs/v1", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType?.MediaType);
        Assert.Contains("MyPoGoCompanion API", await page.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.NotFound, (await offClient.GetAsync(new Uri("/docs/v1", UriKind.Relative), Ct)).StatusCode);
        // The document itself is served everywhere (the smoke test checks it on prod).
        Assert.Equal(HttpStatusCode.OK, (await offClient.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Ct)).StatusCode);
    }

    private static async Task<SessionResponse> ReadSessionAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(_json, Ct))!;
    }

    /// <summary>
    /// The status is documented for this operation, the content type is the documented one (or there's no body when
    /// none is documented), and a JSON body has every property the schema requires. Returns "METHOD /path status".
    /// </summary>
    private static async Task<string> AssertMatchesDocumentAsync(JsonObject document, HttpMethod method, string path, HttpResponseMessage response)
    {
        var bare = path.Split('?')[0];
        var (template, operation) = Operations(document)
            .Where(o => string.Equals(o.Method, method.Method, StringComparison.OrdinalIgnoreCase) && TemplateMatches(o.Path, bare))
            .Select(o => (o.Path, o.Operation))
            .SingleOrDefault();
        Assert.True(operation is not null, $"{method} {bare} isn't in the OpenAPI document");

        var status = ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var documented = operation["responses"]![status];
        Assert.True(documented is not null, $"{method} {template} answered {status}, which the document doesn't list");

        var body = await response.Content.ReadAsStringAsync(Ct);
        if (documented["content"] is not JsonObject content)
        {
            Assert.True(body.Length == 0, $"{method} {template} {status}: the document says no body, the API sent {body}");
        }
        else
        {
            var (mediaType, media) = content.Single();
            Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
            AssertHasRequired(document, media!["schema"]!, JsonNode.Parse(body), $"{method} {template} {status}");
        }

        return $"{method.Method.ToUpperInvariant()} {template} {status}";
    }

    private static void AssertHasRequired(JsonObject document, JsonNode schema, JsonNode? value, string where)
    {
        if (schema["$ref"]?.ToString() is { } reference)
        {
            schema = document["components"]!["schemas"]![reference.Split('/')[^1]]!;
        }

        if (schema["items"] is { } items && value is JsonArray array)
        {
            foreach (var item in array)
            {
                AssertHasRequired(document, items, item, where);
            }

            return;
        }

        foreach (var name in schema["required"]?.AsArray().Select(n => n!.ToString()) ?? [])
        {
            Assert.True(value is JsonObject o && o.ContainsKey(name), $"{where}: the response lacks the required \"{name}\"");
        }
    }

    private static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations(JsonObject document) =>
        document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject()
            .Select(method => (path.Key, method.Key.ToUpperInvariant(), method.Value!.AsObject())));

    private static bool TemplateMatches(string template, string path) =>
        Regex.IsMatch(path, "^" + ParameterPattern().Replace(Regex.Escape(template).Replace("\\{", "{", StringComparison.Ordinal), "[^/]+") + "$");

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd();

    [GeneratedRegex(@"\{[^}/]+\}")]
    private static partial Regex ParameterPattern();
}
