namespace MyPoGoCompanion.Ai;

internal sealed class BudgetedAiClient(IAiProvider provider, AiBudget budget) : IAiClient
{
    public async Task<AiResult> GenerateAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Input);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Instructions);
        if (!Enum.IsDefined(request.Purpose))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Unknown AI purpose.");
        }

        var model = AiModel.For(request.Complexity);
        const int maxOutputTokens = 128_000;
        if (request.MaxOutputTokens is < AiBudget.MinimumOutputTokens or > maxOutputTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(request),
                $"Output tokens must be between {AiBudget.MinimumOutputTokens} and {maxOutputTokens}.");
        }

        if (!provider.IsConfigured)
        {
            throw new AiUnavailableException();
        }

        var id = await budget.ReserveAsync(model, request, cancellationToken);
        ProviderResult result;
        try
        {
            result = await provider.GenerateAsync(model, request, cancellationToken);
        }
        catch (AiProviderException exception) when (exception.DefinitelyUnbilled)
        {
            using var releaseDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await budget.ReleaseAsync(id, releaseDeadline.Token);
            throw;
        }

        // The provider already ran. Persist reported usage even if the caller has disconnected.
        var cost = model.Cost(result.InputTokens, result.CachedInputTokens, result.OutputTokens);
        using var settlementDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await budget.SettleAsync(id, result, cost, settlementDeadline.Token);
        return new AiResult(result.Text, model.Name, result.InputTokens, result.OutputTokens, cost, result.Incomplete);
    }
}
