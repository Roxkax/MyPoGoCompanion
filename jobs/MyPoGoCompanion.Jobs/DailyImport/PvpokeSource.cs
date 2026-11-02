using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MyPoGoCompanion.Jobs.DailyImport;

// PvPoke's files (github.com/pvpoke/pvpoke, MIT): only the fields the import reads.

public sealed class PvpokeGameMaster
{
    public List<PvpokePokemon> Pokemon { get; set; } = [];

    public List<PvpokeMove> Moves { get; set; } = [];
}

public sealed class PvpokePokemon
{
    public int Dex { get; set; }

    public string SpeciesName { get; set; } = "";

    public string SpeciesId { get; set; } = "";

    public PvpokeStats BaseStats { get; set; } = new();

    public List<string> Types { get; set; } = [];

    public List<string> FastMoves { get; set; } = [];

    public List<string> ChargedMoves { get; set; } = [];

    public List<string> EliteMoves { get; set; } = [];

    public List<string> LegacyMoves { get; set; } = [];

    /// <summary>Signature charged moves of some Megas (Fell Stinger+ on Mega Beedrill).</summary>
    public List<string> ExtraChargedMoves { get; set; } = [];

    public List<string> Tags { get; set; } = [];

    public List<string> Nicknames { get; set; } = [];

    [JsonConverter(typeof(LenientIntConverter))]
    public int BuddyDistance { get; set; }

    /// <summary>Stardust for the second charged move; PvPoke writes <c>false</c> for Pokémon that can't get one (0 here).</summary>
    [JsonConverter(typeof(LenientIntConverter))]
    public int ThirdMoveCost { get; set; }

    public bool Released { get; set; }

    public PvpokeFamily? Family { get; set; }

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);
}

public sealed class PvpokeStats
{
    public int Atk { get; set; }

    public int Def { get; set; }

    public int Hp { get; set; }
}

public sealed class PvpokeFamily
{
    public string? Id { get; set; }

    /// <summary>The entry it evolves from (a PvPoke id).</summary>
    public string? Parent { get; set; }

    /// <summary>The entries it evolves into (PvPoke ids).</summary>
    public List<string> Evolutions { get; set; } = [];
}

public sealed class PvpokeMove
{
    public string MoveId { get; set; } = "";

    public string Name { get; set; } = "";

    public string Type { get; set; } = "";

    public double Power { get; set; }

    /// <summary>Energy a charged move costs.</summary>
    public int Energy { get; set; }

    /// <summary>Energy a fast move gains.</summary>
    public int EnergyGain { get; set; }

    public int Turns { get; set; }

    /// <summary>[attack stages, defense stages] for <see cref="BuffTarget"/>.</summary>
    public List<int>? Buffs { get; set; }

    /// <summary><c>self</c>, <c>opponent</c> or <c>both</c> (then <see cref="BuffsSelf"/> and <see cref="BuffsOpponent"/>).</summary>
    public string? BuffTarget { get; set; }

    public List<int>? BuffsSelf { get; set; }

    public List<int>? BuffsOpponent { get; set; }

    /// <summary>A number written as text ("0.3").</summary>
    public string? BuffApplyChance { get; set; }

    public bool IsFast => EnergyGain > 0 || Energy == 0;
}

public sealed class PvpokeRanking
{
    public string SpeciesId { get; set; } = "";

    public double Score { get; set; }

    /// <summary>Fast move, then one or two charged moves.</summary>
    public List<string> Moveset { get; set; } = [];
}

/// <summary>A whole number that PvPoke sometimes writes as <c>false</c>, <c>null</c> or text: 0 unless it is a number.</summary>
public sealed class LenientIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.Number => reader.TryGetInt32(out var value) ? value : (int)Math.Round(reader.GetDouble()),
        JsonTokenType.String => int.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0,
        _ => 0,
    };

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteNumberValue(value);
    }
}

/// <summary>Reads PvPoke's files; a file that doesn't read is a failed run with the file named.</summary>
public static partial class PvpokeSource
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static PvpokeGameMaster ReadGameMaster(SourceFile file)
    {
        var gameMaster = Deserialize<PvpokeGameMaster>(file);
        if (gameMaster.Pokemon.Count == 0 || gameMaster.Moves.Count == 0)
        {
            throw new ImportException($"The {file.Name} has no Pokémon or no moves: its format may have changed.");
        }

        return gameMaster;
    }

    public static List<PvpokeRanking> ReadRankings(SourceFile file) => Deserialize<List<PvpokeRanking>>(file);

    /// <summary>
    /// The CP multiplier per level from <c>Pokemon.js</c> (<c>var cpms = [0.094, …]</c>, one value per half level
    /// from level 1), the table the Python keeper build used.
    /// </summary>
    public static IReadOnlyList<(decimal Level, double Value)> ReadCpMultipliers(SourceFile file, decimal maxLevel)
    {
        var match = CpmsArray().Match(Encoding.UTF8.GetString(file.Content));
        if (!match.Success)
        {
            throw new ImportException($"The {file.Name} has no CP multiplier table (\"var cpms = [...]\"): its format may have changed.");
        }

        var values = new List<(decimal, double)>();
        var level = 1m;
        foreach (var text in match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value <= 0)
            {
                throw new ImportException($"The {file.Name} has a CP multiplier that isn't a number: \"{text}\".");
            }

            if (level <= maxLevel)
            {
                values.Add((level, value));
            }

            level += 0.5m;
        }

        if (values.Count < (int)((maxLevel - 1) * 2) + 1)
        {
            throw new ImportException($"The {file.Name} has CP multipliers only up to level {level - 0.5m}; level {maxLevel} is needed.");
        }

        return values;
    }

    private static T Deserialize<T>(SourceFile file)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(file.Content, _json)
                ?? throw new ImportException($"The {file.Name} is empty.");
        }
        catch (JsonException exception)
        {
            throw new ImportException($"The {file.Name} isn't the JSON the import expects ({exception.Message}).", exception);
        }
    }

    [GeneratedRegex(@"var\s+cpms\s*=\s*\[([^\]]+)\]")]
    private static partial Regex CpmsArray();
}
