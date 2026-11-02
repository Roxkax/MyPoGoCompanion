using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.Operations;

public enum JobKind
{
    DailyImport,
    AiResearcher,
    Backup,
}

public enum JobStatus
{
    Running,
    Succeeded,
    Failed,

    /// <summary>
    /// The daily import found rows the sources no longer list and published nothing: waiting for the super admin to
    /// publish them (a run with "Publish what's missing") or keep yesterday's data (E02-F04).
    /// </summary>
    Held,
}

/// <summary>
/// One run of a scheduled job: the admin's Data status screen, and the out-of-date warning (the last successful
/// run of each job).
/// </summary>
public class JobRun
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public JobKind Job { get; set; }

    public JobStatus Status { get; set; }

    /// <summary>The admin who pressed "Run now"; null for a scheduled run or one started from GitHub.</summary>
    public Guid? RequestedByUserId { get; set; }

    public User? RequestedBy { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>
    /// What the run did (JSON). The daily import writes what started it, its version, each source file's fingerprint,
    /// <c>changes</c> per kind (<c>{"forms": {"added": 3, "changed": 1, "removed": 0}, …}</c>) and its warnings
    /// (docs/DAILY-IMPORT.md).
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>Why it failed, in plain words.</summary>
    public string? Error { get; set; }
}
