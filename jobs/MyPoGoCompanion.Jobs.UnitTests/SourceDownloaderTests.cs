using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class SourceDownloaderTests
{
    private static readonly DailyImportOptions _options = new() { RetryDelay = TimeSpan.Zero, DownloadAttempts = 3 };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SourceDownloader Downloader(HttpMessageHandler handler, DailyImportOptions? options = null) =>
        new(new HttpClient(handler), Options.Create(options ?? _options), NullLogger<SourceDownloader>.Instance);

    [Fact]
    public async Task DownloadAll_FetchesTheSixFiles_FromTheConfiguredAddresses()
    {
        var http = new FakeHttp(Fixtures.Responses(_options.PvpokeBaseUrl, _options.GameMasterUrl));

        var files = await Downloader(http).DownloadAllAsync(Ct);

        Assert.Equal(6, http.Requests.Count);
        Assert.Equal("https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/data/gamemaster.json", files.PvpokeGameMaster.Url.AbsoluteUri);
        Assert.Equal("https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/data/rankings/all/overall/rankings-10000.json", files.RankingsMaster.Url.AbsoluteUri);
        Assert.Equal("https://raw.githubusercontent.com/alexelgt/game_masters/master/GAME_MASTER.json", files.GameMaster.Url.AbsoluteUri);
        Assert.Equal(6, files.All.Count());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Fixtures.Bytes("GAME_MASTER.json"))), files.GameMaster.Sha256);
    }

    [Fact]
    public async Task TemporaryFailures_AreRetried()
    {
        var url = new Uri("https://example.test/file");
        var answers = new Queue<HttpResponseMessage>([
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") },
        ]);
        var http = new FakeHttp(new() { [url.AbsoluteUri] = answers.Dequeue });

        var file = await Downloader(http).DownloadAsync("Test file", url, Ct);

        Assert.Equal(3, http.Requests.Count);
        Assert.Equal("ok"u8.ToArray(), file.Content);
    }

    [Fact]
    public async Task APermanentFailure_FailsAtOnce_NamingTheFile()
    {
        var url = new Uri("https://example.test/missing");
        var http = new FakeHttp([]);

        var exception = await Assert.ThrowsAsync<ImportException>(() => Downloader(http).DownloadAsync("PvPoke game master", url, Ct));

        Assert.Single(http.Requests);
        Assert.Equal("Couldn't download the PvPoke game master (https://example.test/missing): the server answered 404 Not Found.", exception.Message);
    }

    [Fact]
    public async Task RepeatedServerErrors_FailAfterTheLastAttempt()
    {
        var url = new Uri("https://example.test/file");
        var http = new FakeHttp(new() { [url.AbsoluteUri] = () => new HttpResponseMessage(HttpStatusCode.BadGateway) });

        var exception = await Assert.ThrowsAsync<ImportException>(() => Downloader(http).DownloadAsync("Game master", url, Ct));

        Assert.Equal(3, http.Requests.Count);
        Assert.EndsWith("the server answered 502 Bad Gateway, 3 times.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoAnswer_IsRetriedThenFails()
    {
        var http = new Throwing(new HttpRequestException(HttpRequestError.NameResolutionError, "no such host"));

        var exception = await Assert.ThrowsAsync<ImportException>(() =>
            Downloader(http, new DailyImportOptions { RetryDelay = TimeSpan.Zero, DownloadAttempts = 2 }).DownloadAsync("Game master", new Uri("https://example.test/x"), Ct));

        Assert.Equal(2, http.Calls);
        Assert.Contains("no answer (NameResolutionError), 2 times", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASlowServer_TimesOut()
    {
        var http = new Slow();

        var exception = await Assert.ThrowsAsync<ImportException>(() =>
            Downloader(http, new DailyImportOptions { RetryDelay = TimeSpan.Zero, DownloadAttempts = 1, DownloadTimeout = TimeSpan.FromMilliseconds(50) })
                .DownloadAsync("Game master", new Uri("https://example.test/x"), Ct));

        Assert.Contains("no answer within 0 seconds, 1 times", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancelling_StopsTheDownload()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Downloader(new Slow()).DownloadAsync("Game master", new Uri("https://example.test/x"), cancel.Token));
    }

    private sealed class Throwing(Exception exception) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            throw exception;
        }
    }

    private sealed class Slow : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
