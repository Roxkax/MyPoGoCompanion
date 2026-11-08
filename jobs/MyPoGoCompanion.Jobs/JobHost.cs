using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Jobs.DailyImport;
using MyPoGoCompanion.Observability;
using Npgsql;

namespace MyPoGoCompanion.Jobs;

/// <summary>
/// Starts one job: reads the command (the first argument), builds the host with the database, telemetry and the
/// job's services, runs it once and returns the process exit code.
/// </summary>
public static class JobHost
{
    public const string DailyImportCommand = "daily-import";

    public const string SpritesCommand = "sprites";

    /// <summary>Wrong or missing command (the BSD "usage" exit code).</summary>
    public const int UsageExitCode = 64;

    public const string NoDatabase = "No database: set ConnectionStrings__Database (docs/DAILY-IMPORT.md).";

    public const string Usage = "Usage: MyPoGoCompanion.Jobs daily-import [--DailyImport:Trigger=schedule|deploy|manual]\n"
        + "       MyPoGoCompanion.Jobs sprites --source <PokeMiners picture folder> --out <folder> [--set <name>]";

    /// <param name="args">The command, then optional configuration overrides (<c>--Section:Key=value</c>).</param>
    /// <param name="configure">Lets tests replace services (the downloads, the database).</param>
    public static async Task<int> RunAsync(
        string[] args, Action<HostApplicationBuilder>? configure = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length > 0 && args[0] == SpritesCommand)
        {
            return await RunSpritesAsync(args[1..]);
        }

        if (args.Length == 0 || args[0] != DailyImportCommand)
        {
            await Console.Error.WriteLineAsync(Usage);
            return UsageExitCode;
        }

        var builder = Host.CreateApplicationBuilder(args[1..]);
        builder.AddTelemetry(DailyImportCommand);
        // The import writes thousands of rows: EF Core's line per command and the HTTP client's per-request lines
        // would flood the log (and the log workspace's daily cap). The job logs its own steps.
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.Logging.AddFilter("Npgsql", LogLevel.Warning);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        builder.Services.AddJobDatabase(builder.Configuration);
        builder.Services.AddDailyImport(builder.Configuration);
        configure?.Invoke(builder);
        if (!builder.Services.Any(service => service.ServiceType == typeof(AppDbContext)))
        {
            await Console.Error.WriteLineAsync(NoDatabase);
            return 1;
        }

        using var host = builder.Build();
        // Starting the host starts telemetry; stopping it sends what is left before the container ends.
        await host.StartAsync(cancellationToken);
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<DailyImportJob>().RunAsync(cancellationToken);
            return outcome == DailyImportOutcome.Failed ? 1 : 0;
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Builds the web app's pictures from PokeMiners' folder (docs/SPRITES.md); no database or host. Exit code 0 when
    /// it wrote pictures, 1 when it found none or the folder is missing, 64 for wrong arguments.
    /// </summary>
    private static async Task<int> RunSpritesAsync(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            options[args[i]] = args[i + 1];
        }

        if (args.Length % 2 != 0 || !options.TryGetValue("--source", out var source) || !options.TryGetValue("--out", out var output)
            || options.Keys.Any(key => key is not ("--source" or "--out" or "--set")))
        {
            await Console.Error.WriteLineAsync(Usage);
            return UsageExitCode;
        }

        try
        {
            var result = Sprites.SpriteSetBuilder.Build(source, output, options.GetValueOrDefault("--set", "local"));
            await Console.Out.WriteLineAsync(
                $"Wrote {result.Pictures} pictures to {output} ({result.LeftOut} female variants and shiny costume pictures left out, {result.Unreadable} unreadable).");
            foreach (var problem in result.Problems.Take(20))
            {
                await Console.Out.WriteLineAsync($"  Not a picture with something on it: {problem}");
            }

            return result.Pictures > 0 ? 0 : 1;
        }
        catch (DirectoryNotFoundException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    /// <summary>
    /// The database from <c>ConnectionStrings:Database</c> (environment variable <c>ConnectionStrings__Database</c>,
    /// a Container Apps secret in Azure), in either Npgsql's form or Neon's postgresql:// URL.
    /// </summary>
    internal static IServiceCollection AddJobDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Nothing registered: RunAsync stops with a clear message (tests register their own database).
            return services;
        }

        services.AddNpgsqlDataSource(DatabaseConnectionString.Normalize(connectionString));
        services.AddDbContext<AppDbContext>((provider, options) => options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));
        return services;
    }
}
