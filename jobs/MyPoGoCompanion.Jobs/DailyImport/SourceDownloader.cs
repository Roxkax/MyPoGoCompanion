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

/// <summary>
/// Every file one import reads. The game text is optional (decision 2026-11-02): when it can't be downloaded the
/// import goes on, keeps the Portuguese names it has and says why in <see cref="GameTextProblems"/>.
/// </summary>
public sealed record SourceFiles(
    SourceFile PvpokeGameMaster,
    SourceFile CpMultipliers,
    SourceFile RankingsGreat,
    SourceFile RankingsUltra,
    SourceFile RankingsMaster,
    SourceFile GameMaster,
    SourceFile? GameText = null,
    SourceFile? GameTextRemote = null)
{
    /// <summary>Why a game text file is missing, in plain words (becomes a run log warning).</summary>
    public IReadOnlyList<string> GameTextProblems { get; init; } = [];

    public IEnumerable<SourceFile> All =>
        new[] { PvpokeGameMaster, CpMultipliers, RankingsGreat, RankingsUltra, RankingsMaster, GameMaster, GameText, GameTextRemote }
            .OfType<SourceFile>();
}

/// <summary>Downloads the sources over HTTPS, retrying failures that may be temporary.</summary>
public sealed partial class SourceDownloader(HttpClient http, IOptions<DailyImportOptions> options, ILogger<SourceDownloader> logger)
{
    public const string HttpClientName = "sources";

    private readonly DailyImportOptions _options = options.Value;

    public async Task<SourceFiles> DownloadAllAsync(CancellationToken cancellationToken)
    {
        var pvpoke = _options.PvpokeBaseUrl;
        // One after the other: eight files, and GitHub prefers polite clients.
        var pvpokeGameMaster = await DownloadAsync("PvPoke game master", new Uri(pvpoke, "data/gamemaster.json"), cancellationToken);
        var multipliers = await DownloadAsync("PvPoke CP multipliers", new Uri(pvpoke, "js/pokemon/Pokemon.js"), cancellationToken);
        var great = await DownloadAsync("PvPoke Great League rankings", new Uri(pvpoke, "data/rankings/all/overall/rankings-1500.json"), cancellationToken);
        var ultra = await DownloadAsync("PvPoke Ultra League rankings", new Uri(pvpoke, "data/rankings/all/overall/rankings-2500.json"), cancellationToken);
        var master = await DownloadAsync("PvPoke Master League rankings", new Uri(pvpoke, "data/rankings/all/overall/rankings-10000.json"), cancellationToken);
        var gameMaster = await DownloadAsync("Game master", _options.GameMasterUrl, cancellationToken);

        // The Portuguese names are not worth failing the import for: a missing text file is a warning.
        var problems = new List<string>();
        var text = await TryDownloadAsync("Portuguese game text", _options.GameTextUrl, problems, cancellationToken);
        var remote = text is null ? null
            : await TryDownloadAsync("Portuguese game text (server update)", _options.GameTextRemoteUrl, problems, cancellationToken);
        return new SourceFiles(pvpokeGameMaster, multipliers, great, ultra, master, gameMaster, text, remote) { GameTextProblems = problems };
    }

    private async Task<SourceFile?> TryDownloadAsync(string name, Uri url, List<string> problems, CancellationToken cancellationToken)
    {
        try
        {
            return await DownloadAsync(name, url, cancellationToken);
        }
        catch (ImportException exception)
        {
            problems.Add(exception.Message);
            return null;
        }
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

    /// <summary>
    /// Whether PokeMiners' folder has the picture <paramref name="sprite"/> (<c>pm37.fALOLA</c>): a HEAD request, tried
    /// twice. Once GitHub refuses (429, 403) or doesn't answer, every later check of this run is
    /// <see cref="SpriteCheck.Unknown"/>, so a busy GitHub isn't asked thousands more times.
    /// </summary>
    public async Task<SpriteCheck> CheckSpriteAsync(string sprite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        var url = new Uri(_options.SpriteBaseUrl, Uri.EscapeDataString(sprite + SpriteNames.FileSuffix));
        for (var attempt = 1; attempt <= 2 && !_spriteChecksStopped; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_options.SpriteCheckTimeout);
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return SpriteCheck.Exists;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return SpriteCheck.Missing;
                }

                if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
                {
                    StopSpriteChecks($"GitHub answered {(int)response.StatusCode}");
                    break;
                }
            }
            catch (HttpRequestException exception)
            {
                if (attempt == 2)
                {
                    StopSpriteChecks($"no answer ({exception.HttpRequestError})");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == 2)
                {
                    StopSpriteChecks($"no answer within {_options.SpriteCheckTimeout.TotalSeconds:0} seconds");
                }
            }
        }

        return SpriteCheck.Unknown;
    }

    private volatile bool _spriteChecksStopped;

    private void StopSpriteChecks(string problem)
    {
        if (!_spriteChecksStopped)
        {
            _spriteChecksStopped = true;
            LogSpriteChecksStopped(problem);
        }
    }

    private static bool IsTemporary(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloaded the {Name}: {Bytes} bytes, SHA-256 {Fingerprint}…")]
    private partial void LogDownloaded(string name, int bytes, string fingerprint);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloading the {Name} failed ({Problem}); trying again in {Seconds} s.")]
    private partial void LogRetrying(string name, string problem, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stopped checking pictures ({Problem}); the forms not checked keep the pictures they had.")]
    private partial void LogSpriteChecksStopped(string problem);
}
