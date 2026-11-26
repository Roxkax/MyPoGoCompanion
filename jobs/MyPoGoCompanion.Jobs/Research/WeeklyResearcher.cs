using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyPoGoCompanion.Ai;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.Research;

internal sealed partial class WeeklyResearcher(AppDbContext db, IAiClient ai, IResearchSource source, TimeProvider clock,
    ILogger<WeeklyResearcher> logger)
{
    public async Task<int> RunAsync(CancellationToken token)
    {
        var runLock = new PostgresRunLock(db, PostgresRunLock.ResearcherKey);
        await using var lease = await runLock.TryAcquireAsync(token);
        if (lease is null)
        {
            return 0;
        }

        var run = new JobRun { Job = JobKind.AiResearcher, Status = JobStatus.Running, StartedAt = clock.GetUtcNow() };
        var interrupted = await db.JobRuns.Where(r => r.Job == JobKind.AiResearcher && r.Status == JobStatus.Running)
            .ToListAsync(token);
        foreach (var earlier in interrupted)
        {
            earlier.Status = JobStatus.Failed;
            earlier.Error = "The previous researcher stopped before finishing.";
            earlier.FinishedAt = run.StartedAt;
            earlier.Summary = await SummaryAsync(earlier.Id, new(db), token);
        }

        db.JobRuns.Add(run);
        await db.SaveChangesAsync(token);
        try
        {
            var forms = await db.Forms.Select(f => f.Id).ToHashSetAsync(token);
            var observations = new Dictionary<(ResearchTopic, string), Observation>();
            foreach (var topic in Enum.GetValues<ResearchTopic>())
            {
                var results = await source.SearchAsync(topic, cancellationToken: token);
                foreach (var result in results)
                {
                    // Event details carry schedules. The index repeats those events.
                    if (topic == ResearchTopic.Events && result.Url.AbsolutePath.Trim('/') == "events")
                    {
                        continue;
                    }

                    var page = await source.FetchAsync(result.Url, token);
                    var input = await ResearchExtraction.PageTextAsync(page, token);
                    var reply = await ai.GenerateAsync(new(AiPurpose.Researcher, AiComplexity.Standard,
                        ResearchExtraction.Instructions(topic), input, MaxOutputTokens: 12000, JobRunId: run.Id), token);
                    if (reply.Incomplete)
                    {
                        throw new InvalidDataException("Incomplete research response.");
                    }

                    foreach (var (key, fact) in ResearchExtraction.Parse(topic, reply.Text, page.Url, forms))
                    {
                        if (!observations.TryAdd((topic, key), new(topic, key, fact, page.Url, page.RetrievedAt)))
                        {
                            throw new InvalidDataException("Duplicate research entry.");
                        }
                    }
                }
            }

            // A concurrent admin edit causes a serialization failure instead of losing the correction.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var publication = new ResearchPublication(db);
            foreach (var item in observations.Values)
            {
                await publication.ApplyAsync(item, token);
            }

            run.Status = JobStatus.Succeeded;
            run.FinishedAt = clock.GetUtcNow();
            run.Summary = await SummaryAsync(run.Id, publication, token);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return 0;
        }
        catch (Exception error)
        {
            db.ChangeTracker.Clear();
            var failed = await db.JobRuns.SingleAsync(r => r.Id == run.Id, CancellationToken.None);
            if (failed.Status == JobStatus.Succeeded)
            {
                // The transaction committed even though the client lost its acknowledgement.
                return 0;
            }

            failed.Status = JobStatus.Failed;
            failed.FinishedAt = clock.GetUtcNow();
            failed.Error = error switch
            {
                AiUnavailableException => "AI provider is not configured.",
                AiBudgetExceededException => "Monthly AI budget reached.",
                OperationCanceledException => "Run interrupted.",
                _ => $"Research failed ({error.GetType().Name}).",
            };
            failed.Summary = await SummaryAsync(run.Id, new(db), CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            LogFailed(logger, run.Id, failed.Error);
            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Weekly researcher {Run} failed: {Reason}")]
    private static partial void LogFailed(ILogger logger, Guid run, string reason);

    private async Task<string> SummaryAsync(Guid runId, ResearchPublication publication, CancellationToken token)
    {
        var usage = await db.AiUsage.Where(u => u.JobRunId == runId).Select(u => new { u.CostUsd, u.IsReserved }).ToListAsync(token);
        return JsonSerializer.Serialize(new
        {
            added = publication.Added,
            changed = publication.Changed,
            flagged = publication.Flagged,
            costUsd = usage.Where(u => !u.IsReserved).Sum(u => u.CostUsd),
            reservedUsd = usage.Where(u => u.IsReserved).Sum(u => u.CostUsd),
        });
    }
}
