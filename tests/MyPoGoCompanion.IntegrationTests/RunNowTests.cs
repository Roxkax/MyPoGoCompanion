using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The admin's Run now for the daily import, through HTTP with real sign-in and roles (Azure faked).</summary>
public sealed class RunNowTests(PostgresFixture postgres)
{
    private const string RunNow = "/admin/jobs/daily-import/run";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SessionResponse> SignInAsync(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/auth/google",
            new { code, codeVerifier = new string('v', 64), redirectUri = ApiFactory.WebOrigin + "/auth/callback" }, Ct);
        return (await response.Content.ReadFromJsonAsync<SessionResponse>(_json, Ct))!;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string token, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(RunNow, UriKind.Relative)) { Content = JsonContent.Create(body, options: _json) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task AdminsRunTheImport_OnlyTheSuperAdminPublishesWhatsMissing()
    {
        var azure = new FakeImportStarter();
        var google = new FakeGoogle()
            .Add("owner-code", ApiFactory.SuperAdminEmail, "Diogo")
            .Add("misty-code", "misty@example.test", "Misty");
        await using var api = new ApiFactory(await postgres.CreateDatabaseAsync(Ct), google, importStarter: azure);
        using var client = api.CreateClient();
        var owner = await SignInAsync(client, "owner-code");
        var misty = await SignInAsync(client, "misty-code");

        // Misty is a user: no Run now.
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(client, misty.AccessToken, new { publishMissing = false })).StatusCode);
        using (var makeAdmin = new HttpRequestMessage(HttpMethod.Put, new Uri($"/admin/users/{misty.User.Id}/role", UriKind.Relative)))
        {
            makeAdmin.Content = JsonContent.Create(new { role = "Admin" }, options: _json);
            makeAdmin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(makeAdmin, Ct)).StatusCode);
        }

        // As an admin she can run it, but not publish what's missing.
        var started = await PostAsync(client, misty.AccessToken, new { publishMissing = false });
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var run = (await started.Content.ReadFromJsonAsync<RunImportResponse>(_json, Ct))!;
        Assert.Equal(("job-import-test-1", false), (run.Execution, run.PublishMissing));
        var refused = await PostAsync(client, misty.AccessToken, new { publishMissing = true });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("Only the super admin can publish what's missing.", await refused.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(client, owner.AccessToken, new { publishMissing = true })).StatusCode);
        Assert.Equal([new ImportRunRequest(misty.User.Id, false), new ImportRunRequest(owner.User.Id, true)], azure.Requests);
    }
}
