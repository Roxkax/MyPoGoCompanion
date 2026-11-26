using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Ai;

public enum AiComplexity
{
    Simple,
    Standard,
}

/// <summary>One text generation. Callers choose complexity using what they know about the task.</summary>
public sealed record AiRequest(
    AiPurpose Purpose, AiComplexity Complexity, string Instructions, string Input,
    int MaxOutputTokens = 2048, Guid? UserId = null, Guid? JobRunId = null);

public sealed record AiResult(string Text, string Model, int InputTokens, int OutputTokens, decimal CostUsd, bool Incomplete);

public interface IAiClient
{
    Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiBudgetExceededException(DateTimeOffset resetsAt)
    : InvalidOperationException("The shared AI budget cannot cover this request.")
{
    public DateTimeOffset ResetsAt { get; } = resetsAt;
}

public sealed class AiUnavailableException() : InvalidOperationException("The AI provider is not configured.");

public sealed class AiProviderException(string message, bool definitelyUnbilled = false, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool DefinitelyUnbilled { get; } = definitelyUnbilled;
}
