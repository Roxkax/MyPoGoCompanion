using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MyPoGoCompanion.Jobs.Research;

public static class ResearchSourceSetup
{
    public static IServiceCollection AddResearchSource(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IResearchSource, LeekDuckSource>();
        services.AddHttpClient(LeekDuckSource.HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MyPoGoCompanion-research/1.0 (+https://github.com/Roxkax/MyPoGoCompanion)");
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
            });
        return services;
    }
}
