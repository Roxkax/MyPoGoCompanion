using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Operations;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>Starting a job from the command line, its settings and services.</summary>
public sealed class JobHostTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData]
    [InlineData("backup")]
    public async Task AnUnknownCommand_ShowsTheUsage(params string[] args) =>
        Assert.Equal(JobHost.UsageExitCode, await JobHost.RunAsync(args, cancellationToken: Ct));

    [Fact]
    public async Task DailyImport_RunsTheJob_AndReturnsItsOutcome()
    {
        var database = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();
        var exit = await JobHost.RunAsync(["daily-import", "--DailyImport:Trigger=deploy", "--DailyImport:RetryDelay=00:00:00"], builder =>
        {
            builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database, root)
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            builder.Services.AddScoped<IRunLock, NoLock>();
            builder.Services.AddHttpClient<SourceDownloader>(SourceDownloader.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new FakeHttp(Fixtures.Responses(new DailyImportOptions().PvpokeBaseUrl, new DailyImportOptions().GameMasterUrl)));
        }, Ct);

        Assert.Equal(0, exit);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database, root).Options);
        var run = Assert.Single(await db.JobRuns.ToListAsync(Ct));
        Assert.Equal((JobStatus.Succeeded, "deploy"), (run.Status, ImportSummary.TryParse(run.Summary)!.Trigger));
    }

    [Fact]
    public async Task AFailedRun_ExitsWithOne()
    {
        var database = Guid.NewGuid().ToString();
        var exit = await JobHost.RunAsync(["daily-import", "--DailyImport:DownloadAttempts=1"], builder =>
        {
            builder.Services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(database));
            builder.Services.AddScoped<IRunLock, NoLock>();
            builder.Services.AddHttpClient<SourceDownloader>(SourceDownloader.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new FakeHttp([]));
        }, Ct);

        Assert.Equal(1, exit);
    }

    [Fact]
    public void Database_WithoutAConnectionString_IsntRegistered()
    {
        var services = new ServiceCollection().AddJobDatabase(new ConfigurationBuilder().Build());

        Assert.DoesNotContain(services, service => service.ServiceType == typeof(AppDbContext));
    }

    [Fact]
    public void Database_AcceptsNeonsUrl()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "postgresql://user:secret@ep-test.eu-central-1.aws.neon.tech/app?sslmode=require",
        }).Build();
        using var provider = new ServiceCollection().AddJobDatabase(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var connection = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString();

        Assert.Contains("Host=ep-test.eu-central-1.aws.neon.tech", connection, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_BindFromConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DailyImport:Trigger"] = "schedule",
            ["DailyImport:PvpokeBaseUrl"] = "https://mirror.example.test/pvpoke/",
            ["DailyImport:MinimumReleasedFormsShare"] = "0.9",
        }).Build();
        using var provider = new ServiceCollection().AddLogging().AddDailyImport(configuration).BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DailyImportOptions>>().Value;

        Assert.Equal(("schedule", "https://mirror.example.test/pvpoke/", 0.9), (options.Trigger, options.PvpokeBaseUrl.AbsoluteUri, options.MinimumReleasedFormsShare));
        Assert.NotNull(provider.GetRequiredService<SourceDownloader>());
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
    }

    private sealed class NoLock : IRunLock, IAsyncDisposable
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable?>(this);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

public sealed class ImportSummaryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("{ not json")]
    public void TryParse_NothingUsable_IsNull(string? json) => Assert.Null(ImportSummary.TryParse(json));

    [Fact]
    public void TryParse_ReadsWhatARunWrote()
    {
        var summary = ImportSummary.TryParse("""{"trigger":"schedule","version":"abc","unchanged":true,"sources":{"gameMaster":{"sha256":"ff","bytes":3}},"changes":{"forms":{"added":1,"changed":2,"removed":3}}}""")!;

        Assert.Equal(("schedule", "abc", true), (summary.Trigger, summary.Version, summary.Unchanged));
        Assert.Equal(new SourceFingerprint("ff", 3), summary.Sources["gameMaster"]);
        Assert.Equal(new KindChanges(1, 2, 3), summary.Changes["forms"]);
        Assert.Equal(6, summary.Changes["forms"].Total);
    }

    [Fact]
    public void Fingerprints_NameEveryFile() =>
        Assert.Equal(["pvpokeGameMaster", "cpMultipliers", "rankingsGreat", "rankingsUltra", "rankingsMaster", "gameMaster", "gameText", "gameTextRemote"],
            ImportSummary.Fingerprints(Fixtures.Sources()).Keys);

    [Fact]
    public void Fingerprints_LeaveOutAMissingGameText() =>
        Assert.DoesNotContain("gameText", ImportSummary.Fingerprints(Fixtures.Sources() with { GameText = null, GameTextRemote = null }).Keys);

    [Fact]
    public void ImportException_HasTheUsualConstructors()
    {
        Assert.NotNull(new ImportException().Message);
        var inner = new InvalidOperationException("inner");
        Assert.Same(inner, new ImportException("outer", inner).InnerException);
    }
}
