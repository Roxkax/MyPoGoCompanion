using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Ai;

public sealed record AiBudgetStatus(
    decimal CapUsd, decimal SpentUsd, decimal ReservedUsd, DateTimeOffset ResetsAt,
    bool SimpleRequestsPaused, bool StandardRequestsPaused, bool ProviderConfigured);

public interface IAiBudget
{
    Task<AiBudgetStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

internal sealed partial class AiBudget(
    IServiceScopeFactory scopes, IOptions<AiOptions> options, TimeProvider clock,
    IAiProvider provider, ILogger<AiBudget> logger) : IAiBudget
{
    // All app instances and jobs serialize the check and reservation in the shared database.
    private const long BudgetLock = 0x4D50474341494247;
    public const int MinimumOutputTokens = 16;

    public async Task<AiBudgetStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (start, end) = Month(clock.GetUtcNow());
        var (spent, reserved) = await TotalsAsync(db, start, end, cancellationToken);
        var cap = options.Value.MonthlyCapUsd;
        var available = cap - spent - reserved;
        return new AiBudgetStatus(cap, spent, reserved, end,
            available < AiModel.For(AiComplexity.Simple).Reserve(MinimumOutputTokens),
            available < AiModel.For(AiComplexity.Standard).Reserve(MinimumOutputTokens),
            provider.IsConfigured);
    }

    public async Task<Guid> ReserveAsync(AiModel model, AiRequest request, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({BudgetLock})", cancellationToken);

        var now = clock.GetUtcNow();
        var (start, end) = Month(now);
        var (spent, reserved) = await TotalsAsync(db, start, end, cancellationToken);
        var cost = model.Reserve(request.MaxOutputTokens);
        if (spent + reserved + cost > options.Value.MonthlyCapUsd)
        {
            LogBudgetPaused(request.Purpose, spent, reserved, options.Value.MonthlyCapUsd, end);
            throw new AiBudgetExceededException(end);
        }

        var usage = new AiUsage
        {
            At = now,
            Purpose = request.Purpose,
            UserId = request.UserId,
            JobRunId = request.JobRunId,
            Model = model.Name,
            CostUsd = cost,
            IsReserved = true,
        };
        db.AiUsage.Add(usage);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return usage.Id;
    }

    public async Task SettleAsync(Guid id, ProviderResult result, decimal cost, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.AiUsage.Where(usage => usage.Id == id && usage.IsReserved)
            .ExecuteUpdateAsync(set => set
                .SetProperty(usage => usage.InputTokens, result.InputTokens)
                .SetProperty(usage => usage.OutputTokens, result.OutputTokens)
                .SetProperty(usage => usage.CostUsd, cost)
                .SetProperty(usage => usage.IsReserved, false), cancellationToken);
    }

    public async Task ReleaseAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.AiUsage.Where(usage => usage.Id == id && usage.IsReserved).ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<(decimal Spent, decimal Reserved)> TotalsAsync(
        AppDbContext db, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var totals = await db.AiUsage
            .Where(usage => usage.At >= start && usage.At < end)
            .GroupBy(usage => usage.IsReserved)
            .Select(group => new { Reserved = group.Key, Cost = group.Sum(usage => usage.CostUsd) })
            .ToListAsync(cancellationToken);
        return (totals.SingleOrDefault(total => !total.Reserved)?.Cost ?? 0m,
            totals.SingleOrDefault(total => total.Reserved)?.Cost ?? 0m);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Month(DateTimeOffset utc)
    {
        var start = new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI budget declined a {Purpose} request. Spent: {SpentUsd} USD, reserved: {ReservedUsd} USD, cap: {CapUsd} USD. Resets at {ResetsAt}.")]
    private partial void LogBudgetPaused(AiPurpose purpose, decimal spentUsd, decimal reservedUsd, decimal capUsd,
        DateTimeOffset resetsAt);
}
