using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Observability;

namespace MyPoGoCompanion.Jobs.DailyImport;

public enum DailyImportOutcome
{
    /// <summary>The game data now matches the sources.</summary>
    Succeeded,

    /// <summary>The sources and the job are the same as at the last successful run: nothing to do (still a success).</summary>
    Unchanged,

    Failed,

    /// <summary>The sources no longer list some rows: nothing was published, the super admin decides (still exit code 0).</summary>
    Held,

    /// <summary>Another run holds the lock; this one did nothing and left no run log.</summary>
    AlreadyRunning,
}

/// <summary>
/// One run of the daily import (docs/DAILY-IMPORT.md): takes the run lock, logs the run in <c>job_runs</c>,
/// downloads the sources, and — unless they and the job are the same as at the last successful run — rebuilds the
/// game data, runs the sanity checks and saves it in one transaction, so a failed run changes nothing and
/// yesterday's data stays live. A run that would remove rows the sources no longer list is held: it publishes
/// nothing until the super admin accepts exactly those removals (decision 2026-11-02).
/// </summary>
public sealed partial class DailyImportJob(
    AppDbContext db,
    IRunLock runLock,
    SourceDownloader downloader,
    IOptions<DailyImportOptions> options,
    IConfiguration configuration,
    TimeProvider time,
    ILogger<DailyImportJob> logger)
{
    internal const string StoppedMessage = "Stopped before it finished (the job was stopped, timed out or its container restarted).";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly DailyImportOptions _options = options.Value;

    public async Task<DailyImportOutcome> RunAsync(CancellationToken cancellationToken)
    {
        await using var held = await runLock.TryAcquireAsync(cancellationToken);
        if (held is null)
        {
            LogAlreadyRunning();
            return DailyImportOutcome.AlreadyRunning;
        }

        return await RunLockedAsync(cancellationToken);
    }

    private async Task<DailyImportOutcome> RunLockedAsync(CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var startedAt = time.GetUtcNow();

        // This run holds the lock, so a run still marked as running was stopped half-way.
        var stopped = await db.JobRuns.Where(run => run.Job == JobKind.DailyImport && run.Status == JobStatus.Running)
            .ToListAsync(cancellationToken);
        foreach (var earlier in stopped)
        {
            earlier.Status = JobStatus.Failed;
            earlier.FinishedAt = startedAt;
            earlier.Error = StoppedMessage;
        }

        if (stopped.Count > 0)
        {
            LogMarkedStopped(stopped.Count);
        }

        var requestedBy = _options.RequestedBy is { } userId && await db.Users.AnyAsync(user => user.Id == userId, cancellationToken)
            ? userId
            : (Guid?)null;
        var run = new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Running, StartedAt = startedAt, RequestedByUserId = requestedBy };
        db.JobRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        LogStarted(run.Id, _options.Trigger);

        var version = configuration["APP_VERSION"] is { Length: > 0 } v ? v : "dev";
        var summary = new ImportSummary { Trigger = _options.Trigger, Version = version };
        try
        {
            var files = await downloader.DownloadAllAsync(cancellationToken);
            summary.Sources = ImportSummary.Fingerprints(files);

            if (await LastSuccessAsync(run.Id, cancellationToken) is { } last
                && last.Version == version && last.Sources.Count > 0 && SameSources(last.Sources, summary.Sources))
            {
                summary.Unchanged = true;
                await FinishAsync(run, JobStatus.Succeeded, summary, error: null, cancellationToken);
                LogUnchanged(clock.Elapsed.TotalSeconds);
                return DailyImportOutcome.Unchanged;
            }

            var data = GameDataBuilder.Build(files);
            var storedSprites = await db.Forms.AsNoTracking()
                .Select(form => new { form.Id, form.Sprite, form.SpriteShiny, form.SpriteGigantamax })
                .ToDictionaryAsync(form => form.Id, form => new StoredSprites(form.Sprite, form.SpriteShiny, form.SpriteGigantamax),
                    StringComparer.Ordinal, cancellationToken);
            await SpriteResolver.ResolveAsync(data, storedSprites, downloader.CheckSpriteAsync, _options.SpriteCheckParallelism, cancellationToken);
            var pictures = await downloader.ListSpriteFolderAsync(cancellationToken);
            if (pictures.FileNames is { } fileNames)
            {
                Costumes.Add(data, fileNames);
            }
            else
            {
                var species = data.Species.Select(s => s.Dex).ToHashSet();
                data.Costumes.AddRange((await db.Costumes.AsNoTracking().ToListAsync(cancellationToken)).Where(c => species.Contains(c.Dex)));
                data.Warnings.Add($"Costumes not updated, the ones stored are kept: {pictures.Problem}.");
            }

            summary.Warnings = data.Warnings;
            foreach (var warning in data.Warnings)
            {
                LogWarning(warning);
            }

            var storedHighestDex = await db.Species.MaxAsync(species => (int?)species.Dex, cancellationToken) ?? 0;
            summary.Checks = ImportChecks.Run(data, storedHighestDex, _options);
            if (summary.Checks.Any(check => !check.Passed))
            {
                throw new ImportException(ImportChecks.Explain(summary.Checks));
            }

            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                var written = await GameDataWriter.ApplyAsync(db, data, cancellationToken);
                summary.Changes = written.Changes;
                summary.SetRemovals(written.Removals);
                // The first import into a database (no import published yet; the dev seed's sample run doesn't count)
                // has no published data to lose: it publishes.
                var firstImport = (await LastSuccessAsync(run.Id, cancellationToken))?.Sources.Count is null or 0;
                if (written.Removals.Count > 0 && !firstImport && !await AcceptedAsync(run.Id, summary, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                    var held = await db.JobRuns.SingleAsync(r => r.Id == run.Id, cancellationToken);
                    await FinishAsync(held, JobStatus.Held, summary, error: null, cancellationToken);
                    LogHeld(written.Removals.Values.Sum(rows => rows.Count), string.Join(", ", written.Removals.Keys));
                    return DailyImportOutcome.Held;
                }

                summary.PublishedMissing = written.Removals.Count > 0 && !firstImport;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            await FinishAsync(run, JobStatus.Succeeded, summary, error: null, cancellationToken);
            var changed = summary.Changes.Values.Sum(kind => kind.Total);
            LogSucceeded(changed, data.Warnings.Count, clock.Elapsed.TotalSeconds);
            return DailyImportOutcome.Succeeded;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            var error = cancellationToken.IsCancellationRequested ? StoppedMessage : Explain(exception);
            LogFailed(exception, error);

            // Whatever was pending belongs to the rolled-back transaction; only the run log is saved.
            db.ChangeTracker.Clear();
            summary.Changes = [];
            summary.SetRemovals(new Dictionary<string, List<RemovedRow>>());
            var row = await db.JobRuns.SingleAsync(r => r.Id == run.Id, CancellationToken.None);
            await FinishAsync(row, JobStatus.Failed, summary, error, CancellationToken.None);
            return DailyImportOutcome.Failed;
        }
    }

    /// <summary>The reason a run failed, in plain words for the run log, without personal data or secrets.</summary>
    internal static string Explain(Exception exception) => PersonalData.Scrub(exception switch
    {
        ImportException => exception.Message,
        DbUpdateException update => $"Saving the game data failed: {(update.InnerException ?? update).Message}",
        _ => $"Unexpected error ({exception.GetType().Name}): {exception.Message}",
    }) ?? "Unexpected error.";

    /// <summary>
    /// Whether the super admin accepted these removals: the run was started with "Publish what's missing" and the
    /// latest earlier run (published or held) was held with exactly the same removals. Otherwise the run holds again,
    /// with a warning when the acceptance didn't match.
    /// </summary>
    private async Task<bool> AcceptedAsync(Guid currentRunId, ImportSummary summary, CancellationToken cancellationToken)
    {
        if (!_options.PublishMissing)
        {
            return false;
        }

        var latest = await db.JobRuns.AsNoTracking()
            .Where(r => r.Job == JobKind.DailyImport && r.Id != currentRunId && (r.Status == JobStatus.Held || r.Status == JobStatus.Succeeded))
            .OrderByDescending(r => r.StartedAt).ThenByDescending(r => r.Id)
            .Select(r => new { r.Status, r.Summary })
            .FirstOrDefaultAsync(cancellationToken);
        if (latest?.Status == JobStatus.Held
            && ImportSummary.TryParse(latest.Summary)?.RemovalsFingerprint is { } accepted && accepted == summary.RemovalsFingerprint)
        {
            return true;
        }

        summary.Warnings.Add(latest?.Status == JobStatus.Held
            ? "\"Publish what's missing\" wasn't applied: the rows missing changed since the held run you saw. Check the new list."
            : "\"Publish what's missing\" wasn't applied: no run was waiting for a decision. Check the list of what's missing.");
        return false;
    }

    private async Task<ImportSummary?> LastSuccessAsync(Guid currentRunId, CancellationToken cancellationToken)
    {
        var json = await db.JobRuns
            .Where(r => r.Job == JobKind.DailyImport && r.Status == JobStatus.Succeeded && r.Id != currentRunId)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => r.Summary)
            .FirstOrDefaultAsync(cancellationToken);
        return ImportSummary.TryParse(json);
    }

    private static bool SameSources(Dictionary<string, SourceFingerprint> a, Dictionary<string, SourceFingerprint> b) =>
        a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var other) && other.Sha256 == pair.Value.Sha256);

    private async Task FinishAsync(JobRun run, JobStatus status, ImportSummary summary, string? error, CancellationToken cancellationToken)
    {
        run.Status = status;
        run.FinishedAt = time.GetUtcNow();
        run.Summary = JsonSerializer.Serialize(summary, _json);
        run.Error = error;
        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Another daily import is running; this run does nothing.")]
    private partial void LogAlreadyRunning();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Marked {Count} earlier run(s) that never finished as failed.")]
    private partial void LogMarkedStopped(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Daily import {RunId} started ({Trigger}).")]
    private partial void LogStarted(Guid runId, string trigger);

    [LoggerMessage(Level = LogLevel.Information, Message = "The sources and the job haven't changed since the last successful run: nothing to do ({Seconds:0.0} s).")]
    private partial void LogUnchanged(double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Import warning: {Warning}")]
    private partial void LogWarning(string warning);

    [LoggerMessage(Level = LogLevel.Information, Message = "Daily import succeeded: {Changes} rows added, changed or removed, {Warnings} warning(s), {Seconds:0.0} s.")]
    private partial void LogSucceeded(int changes, int warnings, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Daily import held: the sources no longer list {Count} row(s) ({Kinds}); nothing was published until the super admin decides.")]
    private partial void LogHeld(int count, string kinds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Daily import failed: {Reason}")]
    private partial void LogFailed(Exception exception, string reason);
}
