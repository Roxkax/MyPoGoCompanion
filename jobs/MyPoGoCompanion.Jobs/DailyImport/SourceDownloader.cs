using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>A downloaded source file and its fingerprint (SHA-256 of the bytes, lowercase hex).</summary>
public sealed record SourceFile(string Name, Uri Url, byte[] Content)
{
    public string Sha256 { get; } = Convert.ToHexStringLower(SHA256.HashData(Content));
}

/// <summary>Every file one import reads.</summary>
public sealed record SourceFiles(
    SourceFile PvpokeGameMaster,
    SourceFile CpMultipliers,
    SourceFile RankingsGreat,
    SourceFile RankingsUltra,
    SourceFile RankingsMaster,
    SourceFile GameMaster)
{
    public IEnumerable<SourceFile> All =>
        [PvpokeGameMaster, CpMultipliers, RankingsGreat, RankingsUltra, RankingsMaster, GameMaster];
}

/// <summary>Downloads the sources over HTTPS, retrying failures that may be temporary.</summary>
public sealed partial class SourceDownloader(HttpClient http, IOptions<DailyImportOptions> options, ILogger<SourceDownloader> logger)
{
    public const string HttpClientName = "sources";

    private readonly DailyImportOptions _options = options.Value;

    public async Task<SourceFiles> DownloadAllAsync(CancellationToken cancellationToken)
    {
        var pvpoke = _options.PvpokeBaseUrl;
        // One after the other: six files, and GitHub prefers polite clients.
        return new SourceFiles(
            await DownloadAsync("PvPoke game master", new Uri(pvpoke, "data/gamemaster.json"), cancellationToken),
            await DownloadAsync("PvPoke CP multipliers", new Uri(pvpoke, "js/pokemon/Pokemon.js"), cancellationToken),
            await DownloadAsync("PvPoke Great League rankings", new Uri(pvpoke, "data/rankings/all/overall/rankings-1500.json"), cancellationToken),
            await DownloadAsync("PvPoke Ultra League rankings", new Uri(pvpoke, "data/rankings/all/overall/rankings-2500.json"), cancellationToken),
            await DownloadAsync("PvPoke Master League rankings", new Uri(pvpoke, "data/rankings/all/overall/rankings-10000.json"), cancellationToken),
            await DownloadAsync("Game master", _options.GameMasterUrl, cancellationToken));
    }

    public async Task<SourceFile> DownloadAsync(string name, Uri url, CancellationToken cancellationToken)
    {
        var attempts = Math.Max(1, _options.DownloadAttempts);
        var delay = _options.RetryDelay;
        for (var attempt = 1; ; attempt++)
        {
            string problem;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_options.DownloadTimeout);
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync(timeout.Token);
                    var file = new SourceFile(name, url, content);
                    LogDownloaded(name, content.Length, file.Sha256[..12]);
                    return file;
                }

                problem = $"the server answered {(int)response.StatusCode} {response.ReasonPhrase}";
                if (!IsTemporary(response.StatusCode))
                {
                    throw new ImportException($"Couldn't download the {name} ({url}): {problem}.");
                }
            }
            catch (HttpRequestException exception)
            {
                problem = $"no answer ({exception.HttpRequestError})";
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                problem = $"no answer within {_options.DownloadTimeout.TotalSeconds:0} seconds";
            }

            if (attempt >= attempts)
            {
                var tries = attempts == 1 ? "on the only try" : $"{attempts} times";
                throw new ImportException($"Couldn't download the {name} ({url}): {problem}, {tries}.");
            }

            LogRetrying(name, problem, delay.TotalSeconds);
            await Task.Delay(delay, cancellationToken);
            delay *= 2;
        }
    }

    private static bool IsTemporary(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded the {Name}: {Bytes} bytes, SHA-256 {Fingerprint}…")]
    private partial void LogDownloaded(string name, int bytes, string fingerprint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloading the {Name} failed ({Problem}); trying again in {Seconds} s.")]
    private partial void LogRetrying(string name, string problem, double seconds);
}
