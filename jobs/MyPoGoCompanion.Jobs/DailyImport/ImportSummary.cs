using System.Text.Json;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>A source file's fingerprint in the run log.</summary>
public sealed record SourceFingerprint(string Sha256, int Bytes);

/// <summary>
/// What a daily import run did, kept as JSON in <c>job_runs.summary</c> (docs/DAILY-IMPORT.md): what started it,
/// the job's version, each source file's fingerprint, the changes per kind and the warnings.
/// </summary>
public sealed class ImportSummary
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary><c>schedule</c>, <c>deploy</c> or <c>manual</c>.</summary>
    public string Trigger { get; set; } = "";

    /// <summary>The job's commit (<c>APP_VERSION</c>); a new version re-imports even when the sources are the same.</summary>
    public string Version { get; set; } = "";

    /// <summary>True when the sources and the version matched the last successful run, so nothing was rebuilt.</summary>
    public bool Unchanged { get; set; }

    public Dictionary<string, SourceFingerprint> Sources { get; set; } = [];

    /// <summary>Per kind (forms, moves, movepools…): rows added, changed and removed.</summary>
    public Dictionary<string, KindChanges> Changes { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    public static Dictionary<string, SourceFingerprint> Fingerprints(SourceFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return new(StringComparer.Ordinal)
        {
            ["pvpokeGameMaster"] = Of(files.PvpokeGameMaster),
            ["cpMultipliers"] = Of(files.CpMultipliers),
            ["rankingsGreat"] = Of(files.RankingsGreat),
            ["rankingsUltra"] = Of(files.RankingsUltra),
            ["rankingsMaster"] = Of(files.RankingsMaster),
            ["gameMaster"] = Of(files.GameMaster),
        };
    }

    /// <summary>The summary of an earlier run, or null when it has none or it doesn't read (an older format).</summary>
    public static ImportSummary? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ImportSummary>(json, _json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SourceFingerprint Of(SourceFile file) => new(file.Sha256, file.Content.Length);
}
