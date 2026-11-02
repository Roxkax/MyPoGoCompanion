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

/// <summary>Starts one run of the daily import.</summary>
public interface IImportStarter
{
    /// <summary>False when this copy has no job to start (local, tests).</summary>
    bool IsConfigured { get; }

    /// <summary>Starts the run; returns the job execution's name (to find it in the Azure portal).</summary>
    /// <exception cref="JobStartException">Azure refused or didn't answer.</exception>
    Task<string> StartAsync(ImportRunRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Starts the copy's daily import job with the API's own Azure identity (decision 2026-11-02): it reads the job's
/// container (image, settings as secret references) and starts an execution of it with the run's arguments, as
/// <c>.github/scripts/run_import.sh</c> does. The identity needs "Container Apps Jobs Operator" on that job only
/// (<c>infra/grant-run-now.sh</c>, run once per copy by the owner).
/// </summary>
public sealed class AzureImportStarter(HttpClient http, TokenCredential credential, IOptions<RunNowOptions> options) : IImportStarter
{
    public const string HttpClientName = "AzureJobs";

    internal const string ApiVersion = "2024-03-01";

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
