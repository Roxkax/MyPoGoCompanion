using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data.Seed;

namespace MyPoGoCompanion.IntegrationTests;

/// <summary>The development database: the API migrates and seeds it on start-up, once.</summary>
public sealed class DevSeedTests(PostgresFixture postgres)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Seed_EmptyDatabase_AddsTheSampleOnce()
    {
        await using var db = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync(Cancel));
        var now = DateTimeOffset.UtcNow;

        Assert.True(await DevSeed.SeedAsync(db, now, Cancel));
        Assert.False(await DevSeed.SeedAsync(db, now, Cancel));

        var expected = DevSeed.Build(now);
        Assert.Equal(expected.Forms.Count, await db.Forms.CountAsync(Cancel));
        Assert.Equal(expected.KeeperFilters.Count, await db.KeeperFilters.CountAsync(Cancel));
        Assert.Equal(1, await db.Users.CountAsync(Cancel));
        Assert.Equal(expected.CollectionPokemon.Count, await db.CollectionPokemon.CountAsync(Cancel));
    }

    [Fact]
    public async Task Seed_FollowsTheClock_SoTheSampleRaidsAreOnNow()
    {
        await using var db = PostgresFixture.CreateContext(await postgres.CreateDatabaseAsync(Cancel));
        var now = DateTimeOffset.UtcNow;
        await DevSeed.SeedAsync(db, now, Cancel);

        // Wall-clock columns take DateTimes without a kind (Npgsql refuses UTC ones there).
        var today = DateTime.SpecifyKind(now.UtcDateTime.Date.AddHours(18).AddMinutes(30), DateTimeKind.Unspecified);
        var live = await db.RaidBosses.Where(boss => boss.StartsAt <= today && boss.EndsAt > today).Select(boss => boss.FormId).ToListAsync(Cancel);

        Assert.Contains("kyogre", live);
        Assert.Equal(7, live.Count);
    }

    [Fact]
    public async Task Api_StartingInDevelopment_MigratesAndSeedsAnEmptyDatabase()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync(Cancel);
        await using (var factory = new DevelopmentApiFactory(connectionString))
        {
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Cancel);
            response.EnsureSuccessStatusCode();
        }

        await using var db = PostgresFixture.CreateContext(connectionString);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Cancel));
        Assert.True(await db.Users.AnyAsync(user => user.GoogleSubject == DevSeed.SampleGoogleSubject, Cancel));
    }

    [Fact]
    public async Task Api_StartingOutsideDevelopment_LeavesTheDatabaseAlone()
    {
        var connectionString = await postgres.CreateEmptyDatabaseAsync(Cancel);
        await using (var factory = new ApiFactory(connectionString))
        {
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), Cancel);
            response.EnsureSuccessStatusCode();
        }

        await using var db = PostgresFixture.CreateContext(connectionString);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync(Cancel));
    }

    private sealed class DevelopmentApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Database", connectionString);
        }
    }
}
