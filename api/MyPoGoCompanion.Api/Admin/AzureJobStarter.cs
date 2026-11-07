using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace MyPoGoCompanion.Api.Admin;

/// <summary>
/// Where "Run now" starts the daily import, from the <c>RunNow</c> configuration section (<c>RunNow__…</c>, set by the
/// deploys): the copy's own Container Apps Job. Left empty locally and in tests: Run now then answers 503.
/// </summary>
public sealed class RunNowOptions
{
    public const string Section = "RunNow";

    public string? SubscriptionId { get; set; }

    public string? ResourceGroup { get; set; }

    /// <summary><c>job-import-qa</c> or <c>job-import-prod</c>.</summary>
    public string? JobName { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SubscriptionId) && !string.IsNullOrWhiteSpace(ResourceGroup) && !string.IsNullOrWhiteSpace(JobName);
}

/// <summary>A daily import run for the job to do: who asked, and whether to publish what's missing.</summary>
public sealed record ImportRunRequest(Guid RequestedBy, bool PublishMissing);

/// <summary>Azure didn't start the job; the message says why, in plain words.</summary>
public sealed class JobStartException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The job already has an execution starting or running in Azure.</summary>
/// <param name="execution">That execution's name.</param>
public sealed class ImportAlreadyRunningException(string execution)
    : Exception($"A daily import is already starting or running ({execution}).")
{
    public string Execution { get; } = execution;
}

/// <summary>Starts one run of the daily import.</summary>
public interface IImportStarter
{
    /// <summary>False when this copy has no job to start (local, tests).</summary>
    bool IsConfigured { get; }

    /// <summary>Starts the run; returns the job execution's name (to find it in the Azure portal).</summary>
    /// <exception cref="ImportAlreadyRunningException">An execution is already starting or running.</exception>
    /// <exception cref="JobStartException">Azure refused or didn't answer.</exception>
    Task<string> StartAsync(ImportRunRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Starts the copy's daily import job with the API's own Azure identity (decision 2026-11-02): it reads the job's
/// container (image, settings as secret references) and starts an execution of it with the run's arguments, as
/// <c>.github/scripts/run_import.sh</c> does. The identity needs "Container Apps Jobs Operator" on that job only
/// (<c>infra/grant-run-now.sh</c>, run once per copy by the owner). It first asks Azure whether the job already has an
/// execution starting or running: a run's own log only appears once its container has started (20–40 s), so the
/// database alone can't tell during that time.
/// </summary>
public sealed class AzureImportStarter(HttpClient http, TokenCredential credential, IOptions<RunNowOptions> options) : IImportStarter
{
    public const string HttpClientName = "AzureJobs";

    internal const string ApiVersion = "2024-03-01";

    /// <summary>Execution states that mean "starting or running" (Azure's other ones are finished).</summary>
    private static readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase) { "Running", "Processing", "Pending" };

    /// <summary>Pages of executions read at most (Azure keeps about the last hundred).</summary>
    private const int MaxPages = 5;

    private static readonly TokenRequestContext _scope = new(["https://management.azure.com/.default"]);

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<string> StartAsync(ImportRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("Run now isn't set up on this copy.");
        }

        var job = $"https://management.azure.com/subscriptions/{settings.SubscriptionId}/resourceGroups/{settings.ResourceGroup}"
            + $"/providers/Microsoft.App/jobs/{settings.JobName}";
        AccessToken token;
        try
        {
            token = await credential.GetTokenAsync(_scope, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new JobStartException("The API couldn't sign in to Azure with its own identity (is it switched on for this copy?).", exception);
        }

        if (await ActiveExecutionAsync(job, token, cancellationToken) is { } active)
        {
            throw new ImportAlreadyRunningException(active);
        }

        var definition = await SendAsync(HttpMethod.Get, $"{job}?api-version={ApiVersion}", token, body: null, "read the job", cancellationToken);
        var container = (definition["properties"]?["template"]?["containers"] is JsonArray { Count: > 0 } containers ? containers[0]?.AsObject() : null)
            ?? throw new JobStartException($"The job {settings.JobName} has no container to run.");

        // The job's own container with only the arguments changed: its settings come along as they are (secrets are
        // references), so nothing secret passes through the API.
        var run = new JsonObject();
        foreach (var name in new[] { "name", "image", "command", "env", "resources" })
        {
            if (container[name] is { } value)
            {
                run[name] = value.DeepClone();
            }
        }

        var args = new JsonArray("daily-import", "--DailyImport:Trigger=admin",
            "--DailyImport:RequestedBy=" + request.RequestedBy.ToString("D", CultureInfo.InvariantCulture));
        if (request.PublishMissing)
        {
            args.Add("--DailyImport:PublishMissing=true");
        }

        run["args"] = args;
        var started = await SendAsync(HttpMethod.Post, $"{job}/start?api-version={ApiVersion}", token,
            new JsonObject { ["containers"] = new JsonArray(run) }, "start the job", cancellationToken);
        return (string?)started["name"] ?? throw new JobStartException("Azure started the job but didn't say which run it is.");
    }

    /// <summary>The name of an execution that is starting or running, or null when there is none.</summary>
    private async Task<string?> ActiveExecutionAsync(string job, AccessToken token, CancellationToken cancellationToken)
    {
        var url = $"{job}/executions?api-version={ApiVersion}";
        for (var page = 0; page < MaxPages && !string.IsNullOrEmpty(url); page++)
        {
            var executions = await SendAsync(HttpMethod.Get, url, token, body: null, "read the job's runs", cancellationToken);
            foreach (var execution in executions["value"]?.AsArray() ?? [])
            {
                if (execution?["properties"]?["status"]?.GetValue<string>() is { } status && _active.Contains(status))
                {
                    return (string?)execution["name"] ?? "an earlier run";
                }
            }

            url = (string?)executions["nextLink"];
        }

        return null;
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string url, AccessToken token, JsonNode? body, string what, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        if (body is not null)
        {
            message.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new JobStartException($"Azure didn't answer when the API tried to {what}.", exception);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var reason = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized =>
                        "the API's identity isn't allowed to (the owner grants it once: infra/grant-run-now.sh)",
                    System.Net.HttpStatusCode.NotFound => "the job doesn't exist yet (the deploy creates it)",
                    _ => $"Azure answered {(int)response.StatusCode} {response.ReasonPhrase}",
                };
                throw new JobStartException($"Couldn't {what}: {reason}.");
            }

            return JsonNode.Parse(text) ?? new JsonObject();
        }
    }
}
