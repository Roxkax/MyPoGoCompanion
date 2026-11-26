using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyPoGoCompanion.Jobs.Research;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class ResearchSourceTests
{
    [Fact]
    public async Task DiscoversAndFetchesASourcePageAcrossScopesWithTheRegisteredClient()
    {
        var ct = TestContext.Current.CancellationToken;
        var requests = new List<string>();
        const string sourceHtml = "<main><h1>Max Monday</h1></main>";
        var web = WebApplication.CreateBuilder();
        web.WebHost.UseTestServer();
        await using var upstream = web.Build();
        upstream.MapGet("/events/", (HttpContext context) =>
        {
            Record(context);
            return Results.Content("""
                <section class="events-section">
                    <a class="event-item-link" href="/events/max-monday/">
                        <div class="event-item-wrapper max-battles"><h2>Max Monday</h2></div>
                    </a>
                </section>
                """, "text/html");
        });
        upstream.MapGet("/events/max-monday/", (HttpContext context) =>
        {
            Record(context);
            return Results.Content(sourceHtml, "text/html");
        });
        await upstream.StartAsync(ct);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddResearchSource();
        builder.Services.AddHttpClient(LeekDuckSource.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(upstream.GetTestServer().CreateHandler);
        using var jobHost = builder.Build();
        using var searchScope = jobHost.Services.CreateScope();
        using var fetchScope = jobHost.Services.CreateScope();
        var search = searchScope.ServiceProvider.GetRequiredService<IResearchSource>();
        var fetch = fetchScope.ServiceProvider.GetRequiredService<IResearchSource>();

        var match = Assert.Single(await search.SearchAsync(ResearchTopic.MaxBattles, cancellationToken: ct));
        var page = await fetch.FetchAsync(match.Url, ct);
        Assert.Equal("Max Monday", match.Title);
        Assert.Equal(new Uri("https://leekduck.com/events/max-monday/"), page.Url);
        Assert.Equal(sourceHtml, page.Html);
        Assert.Same(page, await search.FetchAsync(match.Url, ct));
        Assert.Equal(["/events/", "/events/max-monday/"], requests);

        void Record(HttpContext context)
        {
            Assert.Equal("leekduck.com", context.Request.Host.Host);
            Assert.Equal("text/html", context.Request.Headers.Accept.ToString());
            Assert.StartsWith("MyPoGoCompanion-research/", context.Request.Headers.UserAgent.ToString(), StringComparison.Ordinal);
            requests.Add(context.Request.Path.Value!);
        }
    }
}
