namespace MyPoGoCompanion.Data.Users;

/// <summary>Where an upload is; the upload screen shows the steps.</summary>
public enum ImportStatus
{
    Queued,
    Reading,
    Matching,
    Comparing,
    Saving,
    Done,
    Failed,
}

/// <summary>
/// One Poke Genie upload and its report. The uploaded file itself is not kept: its rows become the collection,
/// rows that didn't match are kept as <see cref="UnrecognisedRow"/>, and what changed as <see cref="CollectionChange"/>.
/// </summary>
public class CollectionImport
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTimeOffset UploadedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public string? FileName { get; set; }

    public ImportStatus Status { get; set; }

    /// <summary>Why it failed, in plain words.</summary>
    public string? Error { get; set; }

    public int RowCount { get; set; }

    public int ImportedCount { get; set; }

    public int UnrecognisedCount { get; set; }

    public int NewCount { get; set; }

    public int GoneCount { get; set; }

    public int ChangedCount { get; set; }

    public List<UnrecognisedRow> UnrecognisedRows { get; } = [];

    public List<CollectionChange> Changes { get; } = [];
}
