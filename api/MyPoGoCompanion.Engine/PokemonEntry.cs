namespace MyPoGoCompanion.Engine;

/// <summary>
/// One Pokémon as the engine sees it: a PvPoke entry (Shadow versions are entries of their own, <c>_shadow</c>), with
/// what the calculations need. The daily import builds these from the downloaded files; the API from the database.
/// </summary>
public sealed record PokemonEntry
{
    /// <summary>PvPoke's species id (<c>garchomp</c>, <c>garchomp_mega</c>, <c>garchomp_shadow</c>).</summary>
    public required string Id { get; init; }

    public required int Dex { get; init; }

    /// <summary>PvPoke's name (<c>Garchomp (Mega)</c>).</summary>
    public required string Name { get; init; }

    /// <summary>One or two type ids (<c>dragon</c>, <c>ground</c>).</summary>
    public required IReadOnlyList<string> Types { get; init; }

    public required BaseStats Stats { get; init; }

    public bool IsMega { get; init; }

    public bool IsShadow { get; init; }

    public bool Released { get; init; }

    /// <summary>Fast moves (PvPoke ids), Elite TM and Legacy ones included.</summary>
    public IReadOnlyList<string> FastMoves { get; init; } = [];

    /// <summary>Charged moves (PvPoke ids), Elite TM and Legacy ones included.</summary>
    public IReadOnlyList<string> ChargedMoves { get; init; } = [];

    public IReadOnlyList<string> EliteMoves { get; init; } = [];

    /// <summary>The form it evolves from (PvPoke id), if any.</summary>
    public string? Parent { get; init; }

    /// <summary>The forms it evolves into (PvPoke ids).</summary>
    public IReadOnlyList<string> Evolutions { get; init; } = [];

    /// <summary>The Python build's Mega test: PvPoke's <c>mega</c> tag, or <c>_mega</c> / <c>_primal</c> in the id.</summary>
    public static bool IsMegaId(string id, IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tags);
        return tags.Contains("mega", StringComparer.Ordinal)
            || id.Contains("_mega", StringComparison.Ordinal)
            || id.Contains("_primal", StringComparison.Ordinal);
    }

    /// <summary>The Python build's Shadow test: PvPoke's <c>shadow</c> tag, or an id ending in <c>_shadow</c>.</summary>
    public static bool IsShadowId(string id, IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tags);
        return tags.Contains("shadow", StringComparer.Ordinal) || id.EndsWith("_shadow", StringComparison.Ordinal);
    }

    /// <summary>Forms that only exist during a battle: a player can never own one.</summary>
    public static IReadOnlySet<string> BattleOnly { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "aegislash_blade", "mimikyu_busted", "cramorant_gorging", "cramorant_gulping", "morpeko_hangry",
    };

    /// <summary>The form id without <c>_shadow</c> (the database keeps Shadow as a flag).</summary>
    public string FormId => IsShadow && Id.EndsWith("_shadow", StringComparison.Ordinal) ? Id[..^"_shadow".Length] : Id;
}
