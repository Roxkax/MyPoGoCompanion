namespace MyPoGoCompanion.Jobs.Research;

public enum ResearchTopic
{
    Events,
    Raids,
    MaxBattles,
    Eggs,
    FieldResearch,
}

public sealed record ResearchSearchResult(string Title, Uri Url);

/// <summary>The original source HTML and when it was retrieved. Treat page contents as untrusted input.</summary>
public sealed record ResearchPage(Uri Url, string Html, DateTimeOffset RetrievedAt);

/// <summary>Discovers source pages for a topic and retrieves supported source URLs.</summary>
public interface IResearchSource
{
    Task<IReadOnlyList<ResearchSearchResult>> SearchAsync(
        ResearchTopic topic, string? titleQuery = null, CancellationToken cancellationToken = default);

    Task<ResearchPage> FetchAsync(Uri url, CancellationToken cancellationToken = default);
}
