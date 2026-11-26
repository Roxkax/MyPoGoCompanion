namespace MyPoGoCompanion.Ai;

internal sealed class AiOptions
{
    public const string Section = "Ai";
    public string? OpenAiApiKey { get; set; }
    public decimal MonthlyCapUsd { get; set; } = 5m;
}

internal sealed record AiModel(string Name, int ContextTokens, decimal InputPerMillion, decimal CachedInputPerMillion,
    decimal OutputPerMillion)
{
    // Pin the models and rates together. The provider context window bounds the input for any accepted request.
    private static readonly AiModel _nano = new("gpt-5-nano-2025-08-07", 400_000, 0.05m, 0.005m, 0.40m);
    private static readonly AiModel _mini = new("gpt-5-mini-2025-08-07", 400_000, 0.25m, 0.025m, 2.00m);

    public static AiModel For(AiComplexity complexity) => complexity switch
    {
        AiComplexity.Simple => _nano,
        AiComplexity.Standard => _mini,
        _ => throw new ArgumentOutOfRangeException(nameof(complexity)),
    };

    public decimal Reserve(int maxOutputTokens) => Cost(ContextTokens, 0, maxOutputTokens);

    public decimal Cost(int input, int cachedInput, int output)
    {
        var usd = ((input - cachedInput) * InputPerMillion + cachedInput * CachedInputPerMillion
            + output * OutputPerMillion) / 1_000_000m;
        return decimal.Round(usd, 6, MidpointRounding.ToPositiveInfinity);
    }
}
