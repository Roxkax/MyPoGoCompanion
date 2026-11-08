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
    public async Task DownloadAll_FetchesTheEightFiles_FromTheConfiguredAddresses()
    {
        var http = new FakeHttp(Fixtures.Responses(_options.PvpokeBaseUrl, _options.GameMasterUrl));

        var files = await Downloader(http).DownloadAllAsync(Ct);

        Assert.Equal(8, http.Requests.Count);
        Assert.Equal("https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/data/gamemaster.json", files.PvpokeGameMaster.Url.AbsoluteUri);
        Assert.Equal("https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/data/rankings/all/overall/rankings-10000.json", files.RankingsMaster.Url.AbsoluteUri);
        Assert.Equal("https://raw.githubusercontent.com/alexelgt/game_masters/master/GAME_MASTER.json", files.GameMaster.Url.AbsoluteUri);
        Assert.Equal("https://raw.githubusercontent.com/sora10pls/holoholo-text/main/Release/Brazilian%20Portuguese/pt-br_raw.json", files.GameText!.Url.AbsoluteUri);
        Assert.Equal("https://raw.githubusercontent.com/sora10pls/holoholo-text/main/Remote/Brazilian%20Portuguese/pt-br_raw.json", files.GameTextRemote!.Url.AbsoluteUri);
        Assert.Empty(files.GameTextProblems);
        Assert.Equal(8, files.All.Count());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Fixtures.Bytes("GAME_MASTER.json"))), files.GameMaster.Sha256);
    }

    [Fact]
    public async Task AMissingGameText_IsAProblem_NotAFailure()
    {
        var responses = Fixtures.Responses(_options.PvpokeBaseUrl, _options.GameMasterUrl);
        responses.Remove(_options.GameTextUrl.AbsoluteUri);
        var http = new FakeHttp(responses);

        var files = await Downloader(http).DownloadAllAsync(Ct);

        // Without the release's text the server's update is useless, so it isn't asked for.
        Assert.Equal((null, null), (files.GameText, files.GameTextRemote));
        Assert.DoesNotContain(_options.GameTextRemoteUrl.AbsoluteUri, http.Requests);
        Assert.StartsWith("Couldn't download the Portuguese game text (", Assert.Single(files.GameTextProblems), StringComparison.Ordinal);
        Assert.Equal(6, files.All.Count());
    }

    [Fact]
    public async Task AMissingServerUpdate_KeepsTheRelease()
    {
        var responses = Fixtures.Responses(_options.PvpokeBaseUrl, _options.GameMasterUrl);
        responses[_options.GameTextRemoteUrl.AbsoluteUri] = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var files = await Downloader(new FakeHttp(responses)).DownloadAllAsync(Ct);

        Assert.NotNull(files.GameText);
        Assert.Null(files.GameTextRemote);
        Assert.EndsWith("the server answered 500 Internal Server Error, 3 times.", Assert.Single(files.GameTextProblems), StringComparison.Ordinal);
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

        Assert.Contains("no answer within 0 seconds, on the only try", exception.Message, StringComparison.Ordinal);
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

    private static HttpResponseMessage Tree(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static Dictionary<string, Func<HttpResponseMessage>> Trees(string files)
    {
        const string api = "https://api.github.com/repos/PokeMiners/pogo_assets/git/trees/";
        return new()
        {
            [api + "master"] = () => Tree("""{"tree":[{"path":"Images","type":"tree","sha":"a1"},{"path":"README.md","type":"blob","sha":"r"}],"truncated":false}"""),
            [api + "a1"] = () => Tree("""{"tree":[{"path":"Pokemon - 256x256","type":"tree","sha":"b2"}],"truncated":false}"""),
            [api + "b2"] = () => Tree("""{"tree":[{"path":"Addressable Assets","type":"tree","sha":"c3"}],"truncated":false}"""),
            [api + "c3"] = () => Tree(files),
        };
    }

    [Fact]
    public async Task ThePictureList_IsReadFolderByFolder()
    {
        var http = new FakeHttp(Trees("""{"tree":[{"path":"pm25.icon.png","type":"blob","sha":"x"},{"path":"pm25.cFALL_2018.icon.png","type":"blob","sha":"y"},{"path":"old","type":"tree","sha":"z"}],"truncated":false}"""));

        var listing = await Downloader(http).ListSpriteFolderAsync(Ct);

        Assert.Null(listing.Problem);
        Assert.Equal(["pm25.icon.png", "pm25.cFALL_2018.icon.png"], listing.FileNames);
        Assert.Equal(4, http.Requests.Count);
    }

    [Fact]
    public async Task ThePictureList_CutShort_IsAProblem()
    {
        var listing = await Downloader(new FakeHttp(Trees("""{"tree":[],"truncated":true}"""))).ListSpriteFolderAsync(Ct);
        Assert.Null(listing.FileNames);
        Assert.Equal("GitHub cut the picture list short", listing.Problem);
    }

    [Fact]
    public async Task ThePictureList_RefusedOrMissing_IsAProblem_NotAFailure()
    {
        var refused = new FakeHttp(new() { ["https://api.github.com/repos/PokeMiners/pogo_assets/git/trees/master"] = () => new HttpResponseMessage(HttpStatusCode.Forbidden) });
        Assert.Equal("GitHub answered 403 Forbidden", (await Downloader(refused).ListSpriteFolderAsync(Ct)).Problem);

        var responses = Trees("{}");
        responses.Remove("https://api.github.com/repos/PokeMiners/pogo_assets/git/trees/b2");
        responses["https://api.github.com/repos/PokeMiners/pogo_assets/git/trees/a1"] = () => Tree("""{"tree":[],"truncated":false}""");
        Assert.Equal("the folder \"Pokemon - 256x256\" wasn't found in PokeMiners' pictures",
            (await Downloader(new FakeHttp(responses)).ListSpriteFolderAsync(Ct)).Problem);

        var garbled = Trees("{}");
        garbled["https://api.github.com/repos/PokeMiners/pogo_assets/git/trees/master"] = () => Tree("not json");
        Assert.Equal("GitHub's answer couldn't be read", (await Downloader(new FakeHttp(garbled)).ListSpriteFolderAsync(Ct)).Problem);
    }
}
