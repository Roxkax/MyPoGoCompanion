using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Observability;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>
/// Telemetry from the real API on a real database (E01-F08): with Application Insights configured, every request and
/// log goes out without personal data, signed-in requests carry only the user's random id, and the web app's error
/// reports arrive with their reference. What would be sent is caught just before the exporter.
/// </summary>
public sealed class ObservabilityTests(PostgresFixture postgres)
{
    private const string Callback = ApiFactory.WebOrigin + "/auth/callback";
    private const string MistyEmail = "misty@example.test";
    private static readonly string _verifier = new('v', 64);
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    // Nothing listens there: the exporter fails quietly in the background, as it would with Azure unreachable.
    private static readonly Dictionary<string, string?> _insights = new()
    {
        [TelemetrySetup.ConnectionStringSetting] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:9/",
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Telemetry_CarriesNoPersonalData_AndTheUserIdOnly()
    {
        var google = new FakeGoogle().Add("misty-code", MistyEmail, "Misty");
        var captured = new Captured();
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct), google, settings: _insights)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(captured.Register));
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.42");

        var signIn = await client.PostAsJsonAsync(new Uri("/auth/google", UriKind.Relative),
            new { code = "misty-code", codeVerifier = _verifier, redirectUri = Callback }, _json, Ct);
        var session = (await signIn.Content.ReadFromJsonAsync<SessionResponse>(_json, Ct))!;
        using (var me = new HttpRequestMessage(HttpMethod.Get, new Uri("/me?search=" + MistyEmail, UriKind.Relative)))
        {
            me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me, Ct)).StatusCode);
        }

        var report = await client.PostAsJsonAsync(new Uri("/client-errors", UriKind.Relative), new
        {
            reference = "7f3a2c91",
            kind = "Screen",
            message = $"Cannot show {MistyEmail} with token {session.AccessToken}",
            stack = $"at Profile ({MistyEmail})",
            path = "/auth/callback?code=4/0AeanS0a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
            appVersion = "test",
        }, _json, Ct);
        Assert.Equal(HttpStatusCode.Accepted, report.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health/live", UriKind.Relative), Ct)).StatusCode);

        // Requests: the signed-in one carries the user's random id; none has the address, the query or personal data.
        var requests = captured.Requests();
        var meRequest = Assert.Single(requests, r => r.DisplayName == "GET /me");
        Assert.Equal(session.User.Id.ToString(), meRequest.GetTagItem("enduser.id"));
        Assert.DoesNotContain(requests, r => r.DisplayName.Contains("/health/live", StringComparison.Ordinal));
        foreach (var request in requests)
        {
            Assert.Null(request.GetTagItem("client.address"));
            Assert.Null(request.GetTagItem("url.query"));
            AssertClean(string.Join(' ', request.TagObjects.Select(tag => $"{tag.Key}={tag.Value}")), session);
        }

        // Logs: the web app's report arrives with its reference, without the email, token or Google's code.
        var logs = captured.Logs();
        var clientError = Assert.Single(logs, log => log.Contains("Web app error 7f3a2c91 (Screen) on /auth/callback", StringComparison.Ordinal));
        Assert.Contains("[email]", clientError, StringComparison.Ordinal);
        Assert.Contains("[token]", clientError, StringComparison.Ordinal);
        Assert.All(logs, log => AssertClean(log, session));
    }

    [Fact]
    public async Task TooManyErrorReports_Return429()
    {
        await using var api = new ApiFactory(postgres.ConnectionString);
        using var client = api.CreateClient();
        var body = new { reference = "7f3a2c91", kind = "Script", message = "Boom", stack = (string?)null, path = "/", appVersion = "test" };

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync(new Uri("/client-errors", UriKind.Relative), body, _json, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(new Uri("/client-errors", UriKind.Relative), body, _json, Ct)).StatusCode);
    }

    [Fact]
    public async Task WithoutApplicationInsights_NothingIsSentAndTheApiWorks()
    {
        var captured = new Captured();
        await using var api = new ApiFactory(postgres.ConnectionString)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(captured.Register));
        using var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/health", UriKind.Relative), Ct)).StatusCode);
        Assert.Empty(captured.Requests());
    }

    private static void AssertClean(string text, SessionResponse session)
    {
        Assert.DoesNotContain(MistyEmail, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(session.AccessToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain(session.RefreshToken, text, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.42", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0AeanS0a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6", text, StringComparison.Ordinal);
    }

    /// <summary>Catches what would be exported: added after the app's processors, so it sees records already scrubbed.</summary>
    private sealed class Captured
    {
        private readonly ConcurrentQueue<Activity> _requests = new();
        private readonly ConcurrentQueue<string> _logs = new();

        public void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            services.ConfigureOpenTelemetryTracerProvider(traces => traces.AddProcessor(new Catch<Activity>(activity =>
            {
                if (activity.Kind == ActivityKind.Server)
                {
                    _requests.Enqueue(activity);
                }
            })));
            services.ConfigureOpenTelemetryLoggerProvider(logs => logs.AddProcessor(new Catch<LogRecord>(record =>
                _logs.Enqueue(string.Join(' ',
                    record.FormattedMessage,
                    record.Body,
                    string.Join(' ', record.Attributes?.Select(a => $"{a.Key}={a.Value}") ?? []),
                    record.Exception?.ToString())))));
        }

        public List<Activity> Requests() => [.. _requests];

        public List<string> Logs() => [.. _logs];
    }

    private sealed class Catch<T>(Action<T> onEnd) : BaseProcessor<T>
    {
        public override void OnEnd(T data) => onEnd(data);
    }
}
