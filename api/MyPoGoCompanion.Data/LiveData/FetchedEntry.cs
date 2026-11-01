using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.LiveData;

/// <summary>
/// What every entry the weekly AI researcher publishes carries: where it came from, when it was seen and
/// changed, and the admin's fix (which locks it) or removal.
/// </summary>
public abstract class FetchedEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// The researcher's stable name for the entry (e.g. <c>raid/kyogre/five-star/2026-10-27</c>), so a later run
    /// updates it instead of adding a copy.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>The page it came from; shown to the admin only.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>What the source said the last time it was read (JSON), compared on every run.</summary>
    public string? SourceSnapshot { get; set; }

    /// <summary>First published (the admin's "New" flag).</summary>
    public DateTimeOffset FirstFetchedAt { get; set; }

    /// <summary>Last run that saw it.</summary>
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Last run that changed it ("Changed this week").</summary>
    public DateTimeOffset? ChangedAt { get; set; }

    /// <summary>Set when the admin fixes it: the researcher no longer changes it ("Fixed by you").</summary>
    public DateTimeOffset? FixedAt { get; set; }

    public Guid? FixedByUserId { get; set; }

    public User? FixedBy { get; set; }

    /// <summary>Set when the source changes after the fix ("Source changed since your fix").</summary>
    public DateTimeOffset? SourceChangedAt { get; set; }

    /// <summary>Removed by the admin: hidden everywhere, and the researcher doesn't add it again.</summary>
    public DateTimeOffset? RemovedAt { get; set; }
}
