using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Api.Freshness;

/// <summary>
/// The out-of-date warning (spec 16.3): screens show an orange warning where data that stopped updating appears. Read
/// from the last successful run of each job; a failed run (the old data stays live) doesn't count.
/// </summary>
internal static class FreshnessEndpoints
{
    /// <summary>Browsers and proxies may reuse the answer for this long: the warning works in days.</summary>
    internal const int CacheSeconds = 300;

    public static IServiceCollection AddFreshness(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FreshnessOptions>()
            .Bind(configuration.GetSection(FreshnessOptions.Section))
            .Validate(options => options.GameDataDays > 0 && options.LiveDataDays > 0, "Freshness limits must be at least 1 day.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    public static IEndpointRouteBuilder MapFreshnessEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/freshness", GetFreshnessAsync)
            .WithName("GetFreshness")
            .WithTags("Data")
            .WithSummary("Whether game data (daily) and raids and events (weekly) are up to date, for the out-of-date warning.");

        return app;
    }

    internal static async Task<FreshnessResponse> GetFreshnessAsync(
        AppDbContext db, IOptions<FreshnessOptions> options, TimeProvider time, HttpContext http, CancellationToken cancellationToken)
    {
        var lastSuccess = await db.JobRuns
            .Where(run => run.Status == JobStatus.Succeeded && (run.Job == JobKind.DailyImport || run.Job == JobKind.AiResearcher))
            .GroupBy(run => run.Job)
            .Select(group => new { Job = group.Key, FinishedAt = group.Max(run => run.FinishedAt) })
            .ToListAsync(cancellationToken);

        DateTimeOffset? Last(JobKind job) => lastSuccess.SingleOrDefault(row => row.Job == job)?.FinishedAt;

        var now = time.GetUtcNow();
        http.Response.Headers.CacheControl = $"public, max-age={CacheSeconds}";
        return new FreshnessResponse(
            Describe(Last(JobKind.DailyImport), options.Value.GameDataDays, now),
            Describe(Last(JobKind.AiResearcher), options.Value.LiveDataDays, now));
    }

    internal static DataFreshness Describe(DateTimeOffset? lastUpdatedAt, int limitDays, DateTimeOffset now)
    {
        if (lastUpdatedAt is null)
        {
            return new DataFreshness(null, null, OutOfDate: false, limitDays);
        }

        var days = Math.Max(0, (int)Math.Floor((now - lastUpdatedAt.Value).TotalDays));
        return new DataFreshness(lastUpdatedAt, days, days >= limitDays, limitDays);
    }
}
