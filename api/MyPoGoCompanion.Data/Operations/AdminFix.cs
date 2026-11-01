using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.Operations;

public enum AdminAction
{
    Fix,
    Remove,
}

/// <summary>Who changed which fetched entry, when, and from what to what (JSON).</summary>
public class AdminFix
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset At { get; set; }

    public Guid? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>The entry's table, e.g. <c>raid_bosses</c>.</summary>
    public required string EntryTable { get; set; }

    public Guid EntryId { get; set; }

    public AdminAction Action { get; set; }

    public string? Before { get; set; }

    public string? After { get; set; }
}
