using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MyPoGoCompanion.Jobs.Research;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class LeekDuckSourceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly Uri _events = new("https://leekduck.com/events/");
    private static readonly Uri _eggs = new("https://leekduck.com/eggs/");

    [Fact]
    public async Task SearchesCurrentAndUpcomingCardsAndFiltersMaxBattlesByTheirCategory()
    {
        using var http = new FakeHttp(_ => Html("""
            <div class="events-section">
                <a class="event-item-link" href="/events/current"><h2>Current Event</h2></a>
                <a class="event-item-link" href="/events/max-monday/#local">
                    <div class="event-item-wrapper max-mondays"><h2>Max Monday &amp; Friends</h2></div>
                </a>
                <a class="event-item-link" href="/events/max-battle-day/">
                    <div class="event-item-wrapper max-battles"><h2>Max Battle Day</h2></div>
                </a>
                <a class="event-item-link" href="https://leekduck.com/events/max-monday/"><h2>Duplicate Max Monday</h2></a>
                <a class="event-item-link" href="https://elsewhere.example/events/bogus/"><h2>Outside source</h2></a>
                <a class="event-item-link" href="/events/future/"><h2>Future Max Event</h2></a>
                <a class="event-item-link" href="/events/untitled/">Missing title</a>
            </div>
            <a class="event-item-link" href="/events/not-in-list/"><h2>Outside list</h2></a>
            """));
        using var source = new LeekDuckSource(http, TimeProvider.System);

        var events = await source.SearchAsync(ResearchTopic.Events, cancellationToken: Ct);
        var maxBattles = await source.SearchAsync(ResearchTopic.MaxBattles, cancellationToken: Ct);
        var max = await source.SearchAsync(ResearchTopic.MaxBattles, "  MAX monday  ", Ct);
        var matches = await source.SearchAsync(ResearchTopic.Events, "FUTURE", Ct);

        Assert.Equal(["Events", "Current Event", "Max Monday & Friends", "Max Battle Day", "Future Max Event"],
            events.Select(result => result.Title));
        Assert.Equal(["Max Monday & Friends", "Max Battle Day"], maxBattles.Select(result => result.Title));
        Assert.Equal(new ResearchSearchResult("Max Monday & Friends", new Uri(_events, "max-monday/")), Assert.Single(max));
        Assert.Equal("Future Max Event", Assert.Single(matches).Title);
        Assert.Equal(_events, Assert.Single(http.Requests).Url);
    }

    [Theory]
    [InlineData(ResearchTopic.Raids, "Raid Bosses", "raid-bosses/")]
    [InlineData(ResearchTopic.Eggs, "Egg Hatches", "eggs/")]
    [InlineData(ResearchTopic.FieldResearch, "Field Research", "research/")]
    public async Task DedicatedTopicsReturnTheirSourceWithoutFetching(ResearchTopic topic, string title, string path)
    {
        using var http = new FakeHttp(_ => Html(""));
        using var source = new LeekDuckSource(http, TimeProvider.System);

        var results = await source.SearchAsync(topic, title, Ct);
        Assert.Equal(new ResearchSearchResult(title, new Uri(_events, "/" + path)), Assert.Single(results));
        Assert.Empty(await source.SearchAsync(topic, "unrelated", Ct));
        Assert.Empty(http.Requests);
    }

    [Theory]
    [InlineData("http://leekduck.com/events/")]
    [InlineData("https://leekduck.com.evil.example/events/")]
    [InlineData("https://leekduck.com:444/events/")]
    [InlineData("https://user@leekduck.com/events/")]
    [InlineData("https://127.0.0.1/events/")]
    [InlineData("https://leekduck.com/events/?redirect=elsewhere")]
    [InlineData("https://leekduck.com/events/nested/page/")]
    [InlineData("https://leekduck.com/events/x%2Fy/")]
    [InlineData("https://leekduck.com/about/")]
    [InlineData("/events/")]
    public async Task RejectsUnsupportedUrlsBeforeAnyHttpRequest(string address)
    {
        using var http = new FakeHttp(_ => Html(""));
        using var source = new LeekDuckSource(http, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>("url", () => source.FetchAsync(new Uri(address, UriKind.RelativeOrAbsolute), Ct));
        Assert.Empty(http.Requests);
    }

    [Fact]
    public async Task CoalescesConcurrentFetchesAndSpacesRequestsForDifferentPages()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new FakeHttp(async (url, token) =>
        {
            if (url == _events)
            {
                started.SetResult();
                await finish.Task.WaitAsync(token);
            }

            return Html("<h1>Source page</h1>");
        });
        using var source = new LeekDuckSource(http, TimeProvider.System);

        var first = source.FetchAsync(_events, Ct);
        await started.Task.WaitAsync(Ct);
        var samePage = source.FetchAsync(new Uri("https://leekduck.com/events#currently"), Ct);
        var differentPage = source.FetchAsync(_eggs, Ct);
        Assert.Single(http.Requests);
        finish.SetResult();

        var page = await first;
        Assert.Same(page, await samePage);
        Assert.Same(page, await source.FetchAsync(_events, Ct));
        Assert.Equal(_eggs, (await differentPage).Url);

        Assert.Equal([_events, _eggs], http.Requests.Select(request => request.Url));
        Assert.True(TimeProvider.System.GetElapsedTime(http.Requests[0].Timestamp, http.Requests[1].Timestamp) >= TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task RespectsRetryAfterAndKeepsCachedPagesAvailable()
    {
        var clock = new AdjustableClock();
        var calls = 0;
        using var http = new FakeHttp(url =>
        {
            if (url != _eggs || ++calls != 1)
            {
                return Html("<h1>Available</h1>");
            }

            return new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Headers = { RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(2)) },
            };
        });
        using var source = new LeekDuckSource(http, clock);

        var cached = await source.FetchAsync(_events, Ct);
        clock.Advance(TimeSpan.FromSeconds(1));
        var first = await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(_eggs, Ct));
        var refused = await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(_eggs, Ct));
        Assert.Equal(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Equal(2, http.Requests.Count);
        Assert.Same(cached, await source.FetchAsync(_events, Ct));

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("<h1>Available</h1>", (await source.FetchAsync(_eggs, Ct)).Html);
        Assert.Equal(3, http.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.PartialContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task DoesNotCacheOrFollowIncompleteResponses(HttpStatusCode status)
    {
        var clock = new AdjustableClock();
        var calls = 0;
        using var http = new FakeHttp(_ => ++calls == 1
            ? new HttpResponseMessage(status) { Headers = { Location = _eggs } }
            : Html("<h1>Good</h1>"));
        using var source = new LeekDuckSource(http, clock);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(_events, Ct));

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(_events, Assert.Single(http.Requests).Url);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("<h1>Good</h1>", (await source.FetchAsync(_events, Ct)).Html);
        Assert.Equal([_events, _events], http.Requests.Select(request => request.Url));
    }

    [Fact]
    public async Task RejectsNonHtmlAndOversizedHtmlAndCanRetry()
    {
        var clock = new AdjustableClock();
        var calls = 0;
        using var http = new FakeHttp(_ => ++calls switch
        {
            1 => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("text") },
            2 => Html(new string('a', LeekDuckSource.MaxPageBytes + 1)),
            3 => Html("  "),
            _ => Html("<h1>Good</h1>"),
        });
        using var source = new LeekDuckSource(http, clock);

        await Assert.ThrowsAsync<InvalidDataException>(() => source.FetchAsync(_events, Ct));
        clock.Advance(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<HttpRequestException>(() => source.FetchAsync(_events, Ct));
        clock.Advance(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidDataException>(() => source.FetchAsync(_events, Ct));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("<h1>Good</h1>", (await source.FetchAsync(_events, Ct)).Html);
        Assert.Equal(4, http.Requests.Count);
    }

    [Fact]
    public async Task CancelsQueuedFetchWithoutTakingDownTheActiveRequest()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new FakeHttp(async (_, token) =>
        {
            started.SetResult();
            await finish.Task.WaitAsync(token);
            return Html("<h1>Source page</h1>");
        });
        using var source = new LeekDuckSource(http, TimeProvider.System);

        var active = source.FetchAsync(_events, Ct);
        await started.Task.WaitAsync(Ct);
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            var queued = source.FetchAsync(_eggs, cancellation.Token);
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        }
        finally
        {
            finish.SetResult();
        }

        await active;
        Assert.Equal(_events, Assert.Single(http.Requests).Url);
    }

    private static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private sealed class AdjustableClock : TimeProvider
    {
        private TimeSpan _elapsed;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + _elapsed;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _elapsed.Ticks;

        public void Advance(TimeSpan duration) => _elapsed += duration;
    }

    private sealed class FakeHttp(Func<Uri, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler, IHttpClientFactory
    {
        public FakeHttp(Func<Uri, HttpResponseMessage> respond) : this((url, _) => Task.FromResult(respond(url)))
        {
        }

        public List<(Uri Url, long Timestamp)> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!;
            Requests.Add((url, TimeProvider.System.GetTimestamp()));
            return respond(url, cancellationToken);
        }
    }
}
