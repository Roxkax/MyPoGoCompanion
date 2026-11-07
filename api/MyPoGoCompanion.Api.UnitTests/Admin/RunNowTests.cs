using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.UnitTests.Auth;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.UnitTests.Admin;

public sealed class RunNowEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ClaimsPrincipal Signed(Guid id) => new(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));

    private static Task<Results<Accepted<RunImportResponse>, ProblemHttpResult>> RunAsync(
        AuthTestKit kit, User caller, bool publishMissing, IImportStarter starter) =>
        RunNowEndpoints.RunAsync(new RunImportRequest(publishMissing), Signed(caller.Id), kit.Db, starter, kit.OptionsAccessor, kit.Clock, Ct);

    private static ProblemHttpResult Problem(Results<Accepted<RunImportResponse>, ProblemHttpResult> result) =>
        Assert.IsType<ProblemHttpResult>(result.Result);

    [Fact]
    public async Task Admin_StartsARun()
    {
        using var kit = new AuthTestKit();
        var misty = await kit.AddUserAsync("misty@example.com", UserRole.Admin);
        var starter = new Starter();

        var accepted = Assert.IsType<Accepted<RunImportResponse>>((await RunAsync(kit, misty, false, starter)).Result);

        Assert.Equal(new RunImportResponse("run-1", false, kit.Clock.Now), accepted.Value);
        Assert.Equal([new ImportRunRequest(misty.Id, false)], starter.Requests);
    }

    [Fact]
    public async Task PublishWhatsMissing_SuperAdminOnly()
    {
        using var kit = new AuthTestKit();
        var misty = await kit.AddUserAsync("misty@example.com", UserRole.Admin);
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);
        var starter = new Starter();

        var refused = Problem(await RunAsync(kit, misty, true, starter));
        var accepted = Assert.IsType<Accepted<RunImportResponse>>((await RunAsync(kit, owner, true, starter)).Result);

        Assert.Equal((StatusCodes.Status403Forbidden, "Only the super admin can publish what's missing."), (refused.StatusCode, refused.ProblemDetails.Detail));
        Assert.True(accepted.Value!.PublishMissing);
        Assert.Equal([new ImportRunRequest(owner.Id, true)], starter.Requests);
    }

    [Fact]
    public async Task NotSetUp_Returns503()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);

        var problem = Problem(await RunAsync(kit, owner, false, new Starter { IsConfigured = false }));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
    }

    [Theory]
    [InlineData(1, "A daily import is already running (started 1 minute ago).")]
    [InlineData(20, "A daily import is already running (started 20 minutes ago).")]
    public async Task ARunInProgress_Returns409(int minutesAgo, string detail)
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);
        kit.Db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Running, StartedAt = kit.Clock.Now.AddMinutes(-minutesAgo) });
        await kit.Db.SaveChangesAsync(Ct);
        var starter = new Starter();

        var problem = Problem(await RunAsync(kit, owner, false, starter));

        Assert.Equal((StatusCodes.Status409Conflict, detail), (problem.StatusCode, problem.ProblemDetails.Detail));
        Assert.Empty(starter.Requests);
    }

    [Fact]
    public async Task ARunStuckAsRunning_DoesntBlock()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);
        kit.Db.JobRuns.Add(new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Running, StartedAt = kit.Clock.Now.AddHours(-3) });
        kit.Db.JobRuns.Add(new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Running, StartedAt = kit.Clock.Now });
        await kit.Db.SaveChangesAsync(Ct);

        Assert.IsType<Accepted<RunImportResponse>>((await RunAsync(kit, owner, false, new Starter())).Result);
    }

    [Fact]
    public async Task AzureRefusing_Returns502WithTheReason()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);

        var problem = Problem(await RunAsync(kit, owner, false, new Starter { Fail = "Couldn't start the job: Azure answered 500." }));

        Assert.Equal((StatusCodes.Status502BadGateway, "Couldn't start the job: Azure answered 500."), (problem.StatusCode, problem.ProblemDetails.Detail));
    }

    [Fact]
    public async Task AzureSayingARunIsStarting_Returns409()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);

        var problem = Problem(await RunAsync(kit, owner, false, new Starter { Busy = "job-import-qa-abc" }));

        Assert.Equal((StatusCodes.Status409Conflict, "A daily import is already starting or running (job-import-qa-abc)."), (problem.StatusCode, problem.ProblemDetails.Detail));
    }

    private sealed class Starter : IImportStarter
    {
        public List<ImportRunRequest> Requests { get; } = [];

        public bool IsConfigured { get; init; } = true;

        public string? Fail { get; init; }

        public string? Busy { get; init; }

        public Task<string> StartAsync(ImportRunRequest request, CancellationToken cancellationToken)
        {
            if (Fail is not null)
            {
                throw new JobStartException(Fail);
            }

            if (Busy is not null)
            {
                throw new ImportAlreadyRunningException(Busy);
            }

            Requests.Add(request);
            return Task.FromResult($"run-{Requests.Count}");
        }
    }
}

public sealed class AzureImportStarterTests
{
    private const string Job = "https://management.azure.com/subscriptions/sub-1/resourceGroups/rg-mypogocompanion/providers/Microsoft.App/jobs/job-import-qa";
    private static readonly Guid _owner = Guid.Parse("0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly RunNowOptions _configured = new() { SubscriptionId = "sub-1", ResourceGroup = "rg-mypogocompanion", JobName = "job-import-qa" };

    private static string Definition() => new JsonObject
    {
        ["name"] = "job-import-qa",
        ["properties"] = new JsonObject
        {
            ["template"] = new JsonObject
            {
                ["containers"] = new JsonArray(new JsonObject
                {
                    ["name"] = "job-import-qa",
                    ["image"] = "ghcr.io/roxkax/mypogocompanion-jobs:abc",
                    ["args"] = new JsonArray("daily-import"),
                    ["env"] = new JsonArray(new JsonObject { ["name"] = "ConnectionStrings__Database", ["secretRef"] = "database-url" }),
                    ["resources"] = new JsonObject { ["cpu"] = 0.5, ["memory"] = "1Gi" },
                    ["probes"] = new JsonArray(),
                }),
            },
        },
    }.ToJsonString();

    private const string NoneRunning = """{"value":[{"name":"job-import-qa-old1","properties":{"status":"Succeeded"}},{"name":"job-import-qa-old2","properties":{"status":"Failed"}}]}""";

    private static AzureImportStarter Starter(Azure azure, RunNowOptions? options = null, TokenCredential? credential = null) =>
        new(new HttpClient(azure), credential ?? new FakeCredential(), Options.Create(options ?? _configured));

    [Fact]
    public async Task Starts_TheJobsOwnContainer_WithTheRunsArguments()
    {
        var azure = new Azure((HttpStatusCode.OK, NoneRunning), (HttpStatusCode.OK, Definition()), (HttpStatusCode.Accepted, """{"name":"job-import-qa-x1y2z3","id":"/x"}"""));

        var execution = await Starter(azure).StartAsync(new ImportRunRequest(_owner, PublishMissing: true), Ct);

        Assert.Equal("job-import-qa-x1y2z3", execution);
        Assert.Equal([$"GET {Job}/executions?api-version=2024-03-01", $"GET {Job}?api-version=2024-03-01", $"POST {Job}/start?api-version=2024-03-01"], azure.Calls);
        Assert.All(azure.Tokens, token => Assert.Equal("Bearer azure-token", token));
        var container = JsonNode.Parse(azure.Bodies[2]!)!["containers"]![0]!;
        Assert.Equal(
            ["daily-import", "--DailyImport:Trigger=admin", $"--DailyImport:RequestedBy={_owner}", "--DailyImport:PublishMissing=true"],
            container["args"]!.AsArray().Select(a => (string)a!));
        Assert.Equal("ghcr.io/roxkax/mypogocompanion-jobs:abc", (string)container["image"]!);
        Assert.Equal("database-url", (string)container["env"]![0]!["secretRef"]!);
        Assert.Null(container["probes"]);
    }

    [Theory]
    [InlineData("Running")]
    [InlineData("Processing")]
    public async Task AnExecutionStartingOrRunning_IsNotStartedAgain(string status)
    {
        // A run's own log only appears once its container has started, so Azure is asked first.
        var azure = new Azure((HttpStatusCode.OK, """{"value":[{"name":"job-import-qa-done","properties":{"status":"Succeeded"}},{"name":"job-import-qa-busy","properties":{"status":"STATUS"}}]}""".Replace("STATUS", status, StringComparison.Ordinal)));

        var running = await Assert.ThrowsAsync<ImportAlreadyRunningException>(() => Starter(azure).StartAsync(new ImportRunRequest(_owner, false), Ct));

        Assert.Equal(("job-import-qa-busy", "A daily import is already starting or running (job-import-qa-busy)."), (running.Execution, running.Message));
        Assert.Single(azure.Calls);
    }

    [Fact]
    public async Task Executions_AreReadPageByPage()
    {
        var next = $"{Job}/executions?api-version=2024-03-01&$skiptoken=2";
        var azure = new Azure(
            (HttpStatusCode.OK, """{"value":[{"name":"a","properties":{"status":"Succeeded"}}],"nextLink":"NEXT"}""".Replace("NEXT", next, StringComparison.Ordinal)),
            (HttpStatusCode.OK, """{"value":[{"properties":{"status":"Running"}}]}"""));

        var running = await Assert.ThrowsAsync<ImportAlreadyRunningException>(() => Starter(azure).StartAsync(new ImportRunRequest(_owner, false), Ct));

        Assert.Equal("an earlier run", running.Execution);
        Assert.Equal($"GET {next}", azure.Calls[1]);
    }

    [Fact]
    public async Task ANormalRun_DoesntPublishWhatsMissing()
    {
        var azure = new Azure((HttpStatusCode.OK, NoneRunning), (HttpStatusCode.OK, Definition()), (HttpStatusCode.Accepted, """{"name":"run"}"""));

        await Starter(azure).StartAsync(new ImportRunRequest(_owner, PublishMissing: false), Ct);

        Assert.DoesNotContain("PublishMissing", azure.Bodies[2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "Couldn't read the job: the API's identity isn't allowed to (the owner grants it once: infra/grant-run-now.sh).")]
    [InlineData(HttpStatusCode.NotFound, "Couldn't read the job: the job doesn't exist yet (the deploy creates it).")]
    [InlineData(HttpStatusCode.InternalServerError, "Couldn't read the job: Azure answered 500 Internal Server Error.")]
    public async Task AzureRefusing_SaysWhy(HttpStatusCode status, string message)
    {
        var azure = new Azure((HttpStatusCode.OK, NoneRunning), (status, "{}"));

        var failure = await Assert.ThrowsAsync<JobStartException>(() => Starter(azure).StartAsync(new ImportRunRequest(_owner, false), Ct));

        Assert.Equal(message, failure.Message);
    }

    [Fact]
    public async Task RefusedStart_SaysWhy()
    {
        var azure = new Azure((HttpStatusCode.OK, NoneRunning), (HttpStatusCode.OK, Definition()), (HttpStatusCode.Forbidden, "{}"));

        var failure = await Assert.ThrowsAsync<JobStartException>(() => Starter(azure).StartAsync(new ImportRunRequest(_owner, false), Ct));

        Assert.StartsWith("Couldn't start the job: the API's identity isn't allowed to", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoAnswer_NoContainer_NoRunName_AndNoIdentity()
    {
        var silent = new Azure();
        Assert.Equal("Azure didn't answer when the API tried to read the job's runs.",
            (await Assert.ThrowsAsync<JobStartException>(() => Starter(silent).StartAsync(new ImportRunRequest(_owner, false), Ct))).Message);

        var empty = new Azure((HttpStatusCode.OK, NoneRunning), (HttpStatusCode.OK, """{"properties":{"template":{"containers":[]}}}"""));
        Assert.Equal("The job job-import-qa has no container to run.",
            (await Assert.ThrowsAsync<JobStartException>(() => Starter(empty).StartAsync(new ImportRunRequest(_owner, false), Ct))).Message);

        var nameless = new Azure((HttpStatusCode.OK, NoneRunning), (HttpStatusCode.OK, Definition()), (HttpStatusCode.Accepted, "{}"));
        Assert.Equal("Azure started the job but didn't say which run it is.",
            (await Assert.ThrowsAsync<JobStartException>(() => Starter(nameless).StartAsync(new ImportRunRequest(_owner, false), Ct))).Message);

        var noIdentity = new Azure();
        Assert.StartsWith("The API couldn't sign in to Azure with its own identity",
            (await Assert.ThrowsAsync<JobStartException>(() => Starter(noIdentity, credential: new FakeCredential(fail: true)).StartAsync(new ImportRunRequest(_owner, false), Ct))).Message, StringComparison.Ordinal);
        Assert.Empty(noIdentity.Calls);
    }

    [Fact]
    public async Task NotConfigured_IsNotStarted()
    {
        var starter = Starter(new Azure(), new RunNowOptions { SubscriptionId = "sub-1", ResourceGroup = "rg" });

        Assert.False(starter.IsConfigured);
        Assert.True(Starter(new Azure()).IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => starter.StartAsync(new ImportRunRequest(_owner, false), Ct));
    }

    /// <summary>Azure Resource Manager: answers the calls in order; with no answer left, the connection fails.</summary>
    private sealed class Azure(params (HttpStatusCode Status, string Body)[] answers) : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _answers = new(answers);

        public List<string> Calls { get; } = [];

        public List<string?> Tokens { get; } = [];

        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!_answers.TryDequeue(out var answer))
            {
                throw new HttpRequestException("No route to host.");
            }

            Calls.Add($"{request.Method} {request.RequestUri}");
            Tokens.Add(request.Headers.Authorization?.ToString());
            Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(answer.Status) { Content = new StringContent(answer.Body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class FakeCredential(bool fail = false) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            fail ? throw new InvalidOperationException("No managed identity endpoint.") : new AccessToken("azure-token", DateTimeOffset.MaxValue);

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }
}
