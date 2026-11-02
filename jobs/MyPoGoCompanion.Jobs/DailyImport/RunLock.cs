using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>Keeps two runs of a job from overlapping.</summary>
public interface IRunLock
{
    /// <summary>The lock (release it by disposing), or null when another run holds it.</summary>
    Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A PostgreSQL session advisory lock: held by this run's database connection, which stays open until the lock is
/// released, and freed by PostgreSQL if the job dies. Needs a direct connection, not Neon's pooler (the deploys pass
/// the direct host: .github/scripts/deploy_job.sh).
/// </summary>
public sealed class PostgresRunLock(AppDbContext db) : IRunLock
{
    /// <summary>"MPGO" and job 1, the daily import.</summary>
    public const long DailyImportKey = 0x4D50_474F_0000_0001;

    public async Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        if (await QueryAsync("pg_try_advisory_lock", cancellationToken))
        {
            return new Release(this);
        }

        await db.Database.CloseConnectionAsync();
        return null;
    }

    private async Task<bool> QueryAsync(string function, CancellationToken cancellationToken)
    {
        // The function name is one of two constants in this class, never input.
#pragma warning disable EF1002
        return await db.Database.SqlQueryRaw<bool>($"select {function}({{0}}) as \"Value\"", DailyImportKey).SingleAsync(cancellationToken);
#pragma warning restore EF1002
    }

    private Task CloseAsync() => db.Database.CloseConnectionAsync();

    private sealed class Release(PostgresRunLock owner) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await owner.QueryAsync("pg_advisory_unlock", CancellationToken.None);
            }
            finally
            {
                await owner.CloseAsync();
            }
        }
    }
}
