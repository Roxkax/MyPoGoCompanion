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

    /// <summary>The admin who pressed "Run now"; null for a scheduled run.</summary>
    public Guid? RequestedByUserId { get; set; }

    public User? RequestedBy { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>What changed, as counts per kind (JSON), e.g. <c>{"forms": 3, "moves": 1}</c>.</summary>
    public string? Summary { get; set; }

    /// <summary>Why it failed, in plain words.</summary>
    public string? Error { get; set; }
}
