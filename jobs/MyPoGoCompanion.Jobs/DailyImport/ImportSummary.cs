using System.Security.Cryptography;
using System.Text;
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

    /// <summary><c>schedule</c>, <c>deploy</c>, <c>manual</c> or <c>admin</c>.</summary>
    public string Trigger { get; set; } = "";

    /// <summary>The job's commit (<c>APP_VERSION</c>); a new version re-imports even when the sources are the same.</summary>
    public string Version { get; set; } = "";

    /// <summary>True when the sources and the version matched the last successful run, so nothing was rebuilt.</summary>
    public bool Unchanged { get; set; }

    public Dictionary<string, SourceFingerprint> Sources { get; set; } = [];

    /// <summary>Per kind (forms, moves, movepools…): rows added, changed and removed.</summary>
    public Dictionary<string, KindChanges> Changes { get; set; } = [];

    public List<string> Warnings { get; set; } = [];

    /// <summary>The sanity checks and what they found (empty when the run stopped before them).</summary>
    public List<ImportCheck> Checks { get; set; } = [];

    /// <summary>
    /// Per kind, the rows the sources no longer list: how many and the first <see cref="RemovalList.Shown"/> labels.
    /// A held run published nothing because of them; a published run removed them with the super admin's OK.
    /// </summary>
    public Dictionary<string, RemovalList> Removals { get; set; } = [];

    /// <summary>The SHA-256 of every removed row's key: "Publish what's missing" publishes only the removals it saw.</summary>
    public string? RemovalsFingerprint { get; set; }

    /// <summary>True when the run published removals the super admin accepted ("Publish what's missing").</summary>
    public bool PublishedMissing { get; set; }

    public static Dictionary<string, SourceFingerprint> Fingerprints(SourceFiles files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var fingerprints = new Dictionary<string, SourceFingerprint>(StringComparer.Ordinal)
        {
            ["pvpokeGameMaster"] = Of(files.PvpokeGameMaster),
            ["cpMultipliers"] = Of(files.CpMultipliers),
            ["rankingsGreat"] = Of(files.RankingsGreat),
            ["rankingsUltra"] = Of(files.RankingsUltra),
            ["rankingsMaster"] = Of(files.RankingsMaster),
            ["gameMaster"] = Of(files.GameMaster),
        };

        // A missing game text leaves its fingerprint out, so the next run with it doesn't count as "unchanged".
        if (files.GameText is { } text)
        {
            fingerprints["gameText"] = Of(text);
        }

        if (files.GameTextRemote is { } remote)
        {
            fingerprints["gameTextRemote"] = Of(remote);
        }

        return fingerprints;
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

    /// <summary>The run log's removal lists, and the fingerprint of all of them.</summary>
    public void SetRemovals(IReadOnlyDictionary<string, List<RemovedRow>> removals)
    {
        ArgumentNullException.ThrowIfNull(removals);
        Removals = removals.ToDictionary(
            kind => kind.Key,
            kind => new RemovalList(kind.Value.Count, [.. kind.Value.Select(row => row.Label).Order(StringComparer.Ordinal).Take(RemovalList.Shown)]),
            StringComparer.Ordinal);
        RemovalsFingerprint = removals.Count == 0 ? null : Fingerprint(removals);
    }

    /// <summary>The SHA-256 of every removed row's kind and key, in order.</summary>
    public static string Fingerprint(IReadOnlyDictionary<string, List<RemovedRow>> removals)
    {
        ArgumentNullException.ThrowIfNull(removals);
        var lines = removals.SelectMany(kind => kind.Value.Select(row => kind.Key + "\t" + row.Key)).Order(StringComparer.Ordinal);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
    }

    private static SourceFingerprint Of(SourceFile file) => new(file.Sha256, file.Content.Length);
}

/// <summary>The rows of one kind the sources no longer list: how many, and the first labels in order.</summary>
public sealed record RemovalList(int Count, List<string> Items)
{
    /// <summary>Labels kept per kind in the run log (and the GitHub issue).</summary>
    public const int Shown = 20;
}
