using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.Api.Configuration;

internal static partial class DevelopmentDatabase
{
    /// <summary>
    /// Development only: applies the migrations to the local database and, when it is empty, adds the sample data
    /// (<see cref="DevSeed"/>). QA and prod are migrated by their deploy pipelines and never seeded.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Needs a real database; covered by the integration tests.")]
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DevelopmentDatabase));
        await db.Database.MigrateAsync(cancellationToken);
        if (await DevSeed.SeedAsync(db, TimeProvider.System.GetUtcNow(), cancellationToken))
        {
            LogSeeded(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Added the sample data (Ash_PT, Tue 27 Oct 2026 moved to today) to the empty development database.")]
    private static partial void LogSeeded(ILogger logger);
}
