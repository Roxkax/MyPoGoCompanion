namespace MyPoGoCompanion.Data.Users;

/// <summary>Who changed whose role, when, and from what to what. Kept for every grant and removal.</summary>
public class RoleChange
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset At { get; set; }

    /// <summary>The person whose role changed (their history goes with their account).</summary>
    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Who made the change; null once that person's account is deleted.</summary>
    public Guid? ChangedByUserId { get; set; }

    public User? ChangedBy { get; set; }

    public UserRole From { get; set; }

    public UserRole To { get; set; }
}
