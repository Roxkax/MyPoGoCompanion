using System.Security.Claims;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Api.Admin;

/// <summary>Run now's options.</summary>
/// <param name="PublishMissing">
/// "Publish what's missing" (super admin only): publish the rows the sources no longer list, when they are exactly
/// the ones the last held run showed. Left out: a normal run, which holds again if rows are missing.
/// </param>
public sealed record RunImportRequest(bool? PublishMissing);

/// <summary>The run Azure started. The job writes its own run log (job_runs) as soon as it starts.</summary>
/// <param name="Execution">The job execution's name, to find it in the Azure portal.</param>
/// <param name="PublishMissing">Whether it publishes what's missing.</param>
/// <param name="StartedAt">When it was asked to start.</param>
public sealed record RunImportResponse(string Execution, bool PublishMissing, DateTimeOffset StartedAt);

/// <summary>
/// The admin's "Run now" for the daily import (E02-F04; the Data status screen's button comes with E10-F01): starts
/// the copy's Container Apps Job straight away (decision 2026-11-02).
/// </summary>
internal static class RunNowEndpoints
{
    /// <summary>A run still marked running after this long was stopped (the job's own limit is 30 minutes).</summary>
    internal static readonly TimeSpan RunningAtMost = TimeSpan.FromMinutes(35);

    public static IServiceCollection AddRunNow(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RunNowOptions>().Bind(configuration.GetSection(RunNowOptions.Section));
        services.TryAddSingleton(TimeProvider.System);
        // The API's own (system-assigned) Azure identity, only asked for a token when Run now is used.
        services.TryAddSingleton<TokenCredential>(_ => new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned));
        services.AddHttpClient<IImportStarter, AzureImportStarter>(AzureImportStarter.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        return services;
    }

    public static IEndpointRouteBuilder MapRunNowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/jobs/daily-import/run", RunAsync)
            .WithName("RunDailyImport")
            .WithTags("Admin")
            .RequireAuthorization(Policies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithSummary("Starts the daily import now (admins). \"Publish what's missing\" is for the super admin only.");

        return app;
    }

    internal static async Task<Results<Accepted<RunImportResponse>, ProblemHttpResult>> RunAsync(
        RunImportRequest request, ClaimsPrincipal caller, AppDbContext db, IImportStarter starter, IOptions<AuthOptions> auth,
        TimeProvider time, CancellationToken cancellationToken)
    {
        if (!starter.IsConfigured)
        {
            return TypedResults.Problem("Run now isn't set up on this copy (no import job to start here).",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var userId = AccessTokens.UserId(caller) ?? throw new InvalidOperationException("Admin requests are signed in.");
        ArgumentNullException.ThrowIfNull(request);
        var publishMissing = request.PublishMissing == true;
        if (publishMissing)
        {
            var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).SingleOrDefaultAsync(cancellationToken);
            if (!auth.Value.IsSuperAdmin(email))
            {
                return TypedResults.Problem("Only the super admin can publish what's missing.", statusCode: StatusCodes.Status403Forbidden);
            }
        }

        var now = time.GetUtcNow();
        var since = now - RunningAtMost;
        var running = await db.JobRuns.AsNoTracking()
            .Where(run => run.Job == JobKind.DailyImport && run.Status == JobStatus.Running && run.StartedAt > since)
            .OrderByDescending(run => run.StartedAt).Select(run => (DateTimeOffset?)run.StartedAt).FirstOrDefaultAsync(cancellationToken);
        if (running is { } startedAt)
        {
            var minutes = Math.Max(1, (int)(now - startedAt).TotalMinutes);
            return TypedResults.Problem($"A daily import is already running (started {minutes} minute{(minutes == 1 ? "" : "s")} ago).",
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            var execution = await starter.StartAsync(new ImportRunRequest(userId, publishMissing), cancellationToken);
            return TypedResults.Accepted((string?)null, new RunImportResponse(execution, publishMissing, now));
        }
        catch (JobStartException exception)
        {
            return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
