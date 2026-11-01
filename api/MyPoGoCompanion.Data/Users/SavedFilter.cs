namespace MyPoGoCompanion.Data.Users;

/// <summary>A search string the user saved from the filter builder.</summary>
public class SavedFilter
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public required string Name { get; set; }

    public required string SearchString { get; set; }

    /// <summary>The builder's conditions (JSON), so it reopens as conditions; null when written by hand.</summary>
    public string? Conditions { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
