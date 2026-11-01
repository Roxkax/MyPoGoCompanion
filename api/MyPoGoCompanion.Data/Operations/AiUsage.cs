using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Data.Operations;

public enum AiPurpose
{
    Chat,
    Researcher,
}

/// <summary>
/// One call to the AI and what it cost: the monthly spend checked against the cap. Chat messages themselves are
/// not stored (chats last for the session).
/// </summary>
public class AiUsage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset At { get; set; }

    public AiPurpose Purpose { get; set; }

    /// <summary>Who asked (chat); null for the researcher, or once the user is deleted.</summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>The researcher run it belongs to.</summary>
    public Guid? JobRunId { get; set; }

    public JobRun? JobRun { get; set; }

    public required string Model { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public decimal CostUsd { get; set; }
}
