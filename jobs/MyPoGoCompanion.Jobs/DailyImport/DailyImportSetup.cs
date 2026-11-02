using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyPoGoCompanion.Jobs.DailyImport;

public static class DailyImportSetup
{
    /// <summary>The daily import's services: its settings, the downloader's HTTP client and the job.</summary>
    public static IServiceCollection AddDailyImport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DailyImportOptions>().Bind(configuration.GetSection(DailyImportOptions.Section));
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<SourceDownloader>(SourceDownloader.HttpClientName, http =>
            {
                // Each download has its own timeout (DailyImportOptions.DownloadTimeout).
                http.Timeout = Timeout.InfiniteTimeSpan;
                http.DefaultRequestHeaders.UserAgent.ParseAdd("MyPoGoCompanion-daily-import/1.0 (+https://github.com/Roxkax/MyPoGoCompanion)");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            });
        services.AddScoped<IRunLock, PostgresRunLock>();
        services.AddScoped<DailyImportJob>();
        return services;
    }
}
