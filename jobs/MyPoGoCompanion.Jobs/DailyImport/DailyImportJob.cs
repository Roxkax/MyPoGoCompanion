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

    /// <summary>Another run holds the lock; this one did nothing and left no run log.</summary>
    AlreadyRunning,
}

/// <summary>
/// One run of the daily import (docs/DAILY-IMPORT.md): takes the run lock, logs the run in <c>job_runs</c>,
/// downloads the sources, and — unless they and the job are the same as at the last successful run — rebuilds the
/// game data and saves it in one transaction, so a failed run changes nothing and yesterday's data stays live.
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

        var run = new JobRun { Job = JobKind.DailyImport, Status = JobStatus.Running, StartedAt = startedAt };
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
            summary.Warnings = data.Warnings;
            foreach (var warning in data.Warnings)
            {
                LogWarning(warning);
            }

            await CheckReleasedFormsAsync(data, cancellationToken);

            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                summary.Changes = new(await GameDataWriter.ApplyAsync(db, data, cancellationToken), StringComparer.Ordinal);
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
    /// Refuses sources that list far fewer released forms than the database has: a cut-off file would otherwise mark
    /// most Pokémon as not released.
    /// </summary>
    private async Task CheckReleasedFormsAsync(ImportedGameData data, CancellationToken cancellationToken)
    {
        var stored = await db.Forms.CountAsync(form => form.Released, cancellationToken);
        var incoming = data.Forms.Count(form => form.Released);
        if (stored > 0 && incoming < stored * _options.MinimumReleasedFormsShare)
        {
            throw new ImportException(
                $"PvPoke's game master lists {incoming} released forms, far fewer than the {stored} already stored "
                + $"(at least {_options.MinimumReleasedFormsShare:P0} expected): it looks incomplete, so nothing was imported.");
        }
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Daily import failed: {Reason}")]
    private partial void LogFailed(Exception exception, string reason);
}
