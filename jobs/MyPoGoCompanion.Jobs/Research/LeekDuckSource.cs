using System.Buffers;
using System.Net;
using System.Text;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Caching.Memory;

namespace MyPoGoCompanion.Jobs.Research;

internal sealed class LeekDuckSource(IHttpClientFactory clients, TimeProvider clock) : IResearchSource, IDisposable
{
    internal const string HttpClientName = "LeekDuck";
    internal const int MaxPageBytes = 1024 * 1024;

    private static readonly Uri _site = new("https://leekduck.com/");
    private static readonly SearchValues<char> _eventSlugCharacters = SearchValues.Create("abcdefghijklmnopqrstuvwxyz0123456789-_");
    private static readonly TimeSpan _requestSpacing = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _defaultCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _fetchTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(15);
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 });
    private readonly SemaphoreSlim _requestGate = new(1);
    private long? _lastRequest;
    private DateTimeOffset _cooldownUntil;

    public async Task<IReadOnlyList<ResearchSearchResult>> SearchAsync(
        ResearchTopic topic, string? titleQuery = null, CancellationToken cancellationToken = default)
    {
        var (path, title) = topic switch
        {
            ResearchTopic.Events or ResearchTopic.MaxBattles => ("events/", "Events"),
            ResearchTopic.Raids => ("raid-bosses/", "Raid Bosses"),
            ResearchTopic.Eggs => ("eggs/", "Egg Hatches"),
            ResearchTopic.FieldResearch => ("research/", "Field Research"),
            _ => throw new ArgumentOutOfRangeException(nameof(topic)),
        };
        cancellationToken.ThrowIfCancellationRequested();
        var url = new Uri(_site, path);
        ResearchSearchResult index = new(title, url);
        var query = titleQuery?.Trim();
        if (topic is not (ResearchTopic.Events or ResearchTopic.MaxBattles))
        {
            return Matches(title, query) ? [index] : [];
        }

        var page = await FetchAsync(url, cancellationToken);
        var document = await new HtmlParser().ParseDocumentAsync(page.Html, cancellationToken);
        const string eventCards = ".events-section a.event-item-link";
        var selector = topic == ResearchTopic.MaxBattles
            ? eventCards + ":has(.event-item-wrapper.max-battles, .event-item-wrapper.max-mondays)"
            : eventCards;
        var results = new List<ResearchSearchResult>();
        if (topic == ResearchTopic.Events && Matches(title, query))
        {
            results.Add(index);
        }

        var seen = new HashSet<Uri> { url };
        foreach (var link in document.QuerySelectorAll(selector))
        {
            var eventTitle = link.QuerySelector("h2")?.TextContent.Trim();
            if (!string.IsNullOrEmpty(eventTitle) && Matches(eventTitle, query)
                && Uri.TryCreate(url, link.GetAttribute("href"), out var target)
                && CanonicalUrl(target) is { } eventUrl && IsEventPage(eventUrl.AbsolutePath)
                && seen.Add(eventUrl))
            {
                results.Add(new(eventTitle, eventUrl));
            }
        }

        return results;
    }

    public async Task<ResearchPage> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var canonical = CanonicalUrl(url) ?? throw new ArgumentException("Use a supported Leek Duck source URL.", nameof(url));
        cancellationToken.ThrowIfCancellationRequested();
        if (_cache.TryGetValue<ResearchPage>(canonical, out var cached) && cached is not null)
        {
            return cached;
        }

        // One gate covers cache misses, including callers waiting on the same URL.
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(canonical, out cached) && cached is not null)
            {
                return cached;
            }

            if (clock.GetUtcNow() < _cooldownUntil)
            {
                throw new HttpRequestException("Leek Duck requested a cooldown.", null, HttpStatusCode.TooManyRequests);
            }

            if (_lastRequest is long timestamp)
            {
                var wait = _requestSpacing - clock.GetElapsedTime(timestamp);
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, clock, cancellationToken);
                }
            }

            _lastRequest = clock.GetTimestamp();
            var page = await DownloadAsync(canonical, cancellationToken);
            _cache.Set(canonical, page, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _cacheLifetime, Size = 1 });
            return page;
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task<ResearchPage> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        using var client = clients.CreateClient(HttpClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_fetchTimeout);
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || (response.StatusCode == HttpStatusCode.ServiceUnavailable && response.Headers.RetryAfter is not null))
            {
                var now = clock.GetUtcNow();
                var retry = response.Headers.RetryAfter;
                var until = retry?.Date ?? now + (retry?.Delta ?? _defaultCooldown);
                _cooldownUntil = until > now ? until : now + _defaultCooldown;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new HttpRequestException("Leek Duck did not return a complete page.", null, response.StatusCode);
            }

            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Leek Duck did not return HTML.");
            }

            // LoadIntoBufferAsync caps the decompressed body, including responses without Content-Length.
            await response.Content.LoadIntoBufferAsync(MaxPageBytes, timeout.Token);
            var html = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync(timeout.Token));
            if (string.IsNullOrWhiteSpace(html))
            {
                throw new InvalidDataException("Leek Duck returned an empty page.");
            }

            return new ResearchPage(url, html, clock.GetUtcNow());
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("Leek Duck did not return its page before the request timed out.", exception);
        }
    }

    private static bool Matches(string title, string? query) =>
        string.IsNullOrEmpty(query) || title.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static Uri? CanonicalUrl(Uri url)
    {
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps || url.Host != _site.Host
            || !url.IsDefaultPort || url.UserInfo.Length > 0 || url.Query.Length > 0)
        {
            return null;
        }

        var path = url.AbsolutePath;
        if (!path.EndsWith('/'))
        {
            path += "/";
        }

        return path is "/events/" or "/raid-bosses/" or "/eggs/" or "/research/" || IsEventPage(path)
            ? new Uri(_site, path)
            : null;
    }

    private static bool IsEventPage(string path)
    {
        const string prefix = "/events/";
        return path.StartsWith(prefix, StringComparison.Ordinal) && path.Length > prefix.Length + 1
            && path.AsSpan(prefix.Length, path.Length - prefix.Length - 1)
                .IndexOfAnyExcept(_eventSlugCharacters) < 0;
    }

    public void Dispose()
    {
        _cache.Dispose();
        _requestGate.Dispose();
    }
}
