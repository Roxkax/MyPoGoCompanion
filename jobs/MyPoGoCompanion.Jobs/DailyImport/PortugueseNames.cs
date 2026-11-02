using System.Globalization;
using System.Text.RegularExpressions;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// Gives species, forms and moves their Portuguese names from the game's text (E02-F02, spec 16.2; the game's
/// Portuguese is Brazilian, decision 2026-11-01). A name the text doesn't have stays null, the app shows the English
/// one, and the run log lists it. Types are named where the game data is built.
/// <list type="bullet">
/// <item>Species: <c>pokemon_name_0006</c>.</item>
/// <item>Megas and Primals: the game's own names (<c>pokemon_name_0006_0002</c> "Mega Charizard X",
/// <c>pokemon_name_0382_0004</c> "Kyogre Primitivo").</item>
/// <item>Regional forms: "Vulpix de Alola"; with another form on top, its form text in brackets.</item>
/// <item>Other forms: the species and the game's form text in brackets ("Giratina (Forma Alterada)"); the form text
/// is found from the game master's form name (<c>form_giratina_altered</c> or <c>form_altered</c>).</item>
/// <item>A form text that already names the Pokémon is the name ("Kyurem Branco", not "Kyurem (Kyurem Branco)").</item>
/// <item>Without a form text for the game master's name, PvPoke's bracket word is tried (<c>form_male</c> "Macho").</item>
/// <item>A species with one form uses the species name, whatever PvPoke puts in brackets ("Type (Null)" → "Tipo Nulo").</item>
/// <item>Moves: <c>move_name_0013</c>, by the game master's move number; PvPoke's own moves are built from the move they
/// stand for: "Surfar+", "Poder Oculto (Inseto)", Aegislash's charge moves.</item>
/// </list>
/// </summary>
public static partial class PortugueseNames
{
    /// <summary>How the game's Portuguese names regional forms ("Decidueye de Hisui").</summary>
    private static readonly (string IdPart, string English, string Portuguese)[] _regions =
    [
        ("_alolan", "Alolan", "de Alola"),
        ("_galarian", "Galarian", "de Galar"),
        ("_hisuian", "Hisuian", "de Hisui"),
        ("_paldean", "Paldean", "de Paldea"),
    ];

    public static void Apply(ImportedGameData data, GameText text, GameMasterSource gameMaster)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(gameMaster);

        var speciesNames = new Dictionary<int, string?>();
        foreach (var species in data.Species)
        {
            species.NamePt = text.Get(PokemonKey(species.Dex));
            speciesNames[species.Dex] = species.NamePt;
        }

        var formsPerDex = data.Forms.Where(form => form.Kind is not (FormKind.Mega or FormKind.Primal))
            .GroupBy(form => form.Dex).ToDictionary(group => group.Key, group => group.Count());
        foreach (var form in data.Forms)
        {
            form.NamePt = FormName(form, speciesNames.GetValueOrDefault(form.Dex), formsPerDex.GetValueOrDefault(form.Dex), text, gameMaster);
        }

        var typeNames = data.Types.ToDictionary(type => type.Id, type => type.NamePt, StringComparer.Ordinal);
        foreach (var move in data.Moves)
        {
            move.NamePt = MoveName(move.Id, move.Kind == MoveKind.Fast, text, gameMaster, typeNames);
        }

        Missing(data, data.Species.Where(s => s.NamePt is null).Select(s => $"#{s.Dex} {s.NameEn}"), "species");
        Missing(data, data.Forms.Where(f => f.NamePt is null && speciesNames.GetValueOrDefault(f.Dex) is not null).Select(f => f.Id), "forms");
        Missing(data, data.Moves.Where(m => m.NamePt is null).Select(m => m.Id), "moves");
    }

    internal static string PokemonKey(int dex) => string.Create(CultureInfo.InvariantCulture, $"pokemon_name_{dex:D4}");

    internal static string? FormName(Form form, string? species, int formsOfSpecies, GameText text, GameMasterSource gameMaster)
    {
        if (form.Kind is FormKind.Mega or FormKind.Primal)
        {
            return text.Get($"{PokemonKey(form.Dex)}_{MegaCode(form)}");
        }

        if (species is null)
        {
            return null;
        }

        var brackets = Brackets().Match(form.NameEn);
        if (!brackets.Success || formsOfSpecies == 1)
        {
            return species;
        }

        var region = _regions.FirstOrDefault(r => form.Id.Contains(r.IdPart, StringComparison.Ordinal));
        var rest = brackets.Groups[1].Value;
        if (region.English is not null)
        {
            rest = rest.Replace(region.English, "", StringComparison.Ordinal).Trim();
            species = $"{species} {region.Portuguese}";
            if (rest.Length == 0)
            {
                return species;
            }
        }

        var label = FormLabel(form.GameMasterId, text, gameMaster) ?? text.Get("form_" + Words().Replace(rest, "_").ToLowerInvariant());
        return label is null ? null
            // The game's text sometimes names the whole Pokémon ("Kyurem Branco", "Rotom Ventilador"): used as it is.
            : label.Contains(species, StringComparison.Ordinal) ? label
            : $"{species} ({label})";
    }

    /// <summary>The game's text for a form ("Forma Alterada"), from its game master name, or null.</summary>
    internal static string? FormLabel(string? gameMasterId, GameText text, GameMasterSource gameMaster)
    {
        if (gameMasterId is null || gameMaster.Pokemon.GetValueOrDefault(gameMasterId) is not { Form: { } form } template)
        {
            return null;
        }

        var suffix = form.StartsWith(template.PokemonId + "_", StringComparison.Ordinal) ? form[(template.PokemonId.Length + 1)..] : form;
        // Regional forms are named by their region already: "DARMANITAN_GALARIAN_ZEN" is looked up as its Zen form too.
        var withoutRegion = RegionPart().Replace(suffix, "");
        string[] keys =
        [
            "form_" + form, "form_" + suffix,
            "form_" + template.PokemonId + "_" + withoutRegion, "form_" + withoutRegion,
        ];
        return keys.Select(key => text.Get(key.ToLowerInvariant())).FirstOrDefault(label => label is not null);
    }

    internal static string? MoveName(string moveId, bool fast, GameText text, GameMasterSource gameMaster, IReadOnlyDictionary<string, string> typeNames)
    {
        if (GameName(moveId, fast) is { } name)
        {
            return name;
        }

        // PvPoke's moves without a game move of their own: named after the move they stand for.
        if (PlusMove().Match(moveId) is { Success: true } plus)
        {
            return GameName(plus.Groups[1].Value, fast) is { } baseName ? baseName + "+" : null;
        }

        if (HiddenPower().Match(moveId) is { Success: true } hidden)
        {
            var type = hidden.Groups[1].Value.ToLowerInvariant();
            return GameName("HIDDEN_POWER", fast) is not { } power ? null
                : type == "normal" ? power
                : typeNames.TryGetValue(type, out var typeName) ? $"{power} ({typeName})" : null;
        }

        return AegislashCharge().Match(moveId) is { Success: true } aegislash ? GameName(aegislash.Groups[1].Value, fast) : null;

        string? GameName(string id, bool isFast) =>
            gameMaster.MoveNumbers.TryGetValue(GameDataBuilder.GameMasterMoveKey(id, isFast), out var number)
            || gameMaster.MoveNumbers.TryGetValue(GameDataBuilder.GameMasterMoveKey(id, !isFast), out number)
                ? text.Get(string.Create(CultureInfo.InvariantCulture, $"move_name_{number:D4}"))
                : null;
    }

    /// <summary>The game's number for a Mega or Primal look: 0001 Mega, 0002 Mega X, 0003 Mega Y, 0004 Primal.</summary>
    private static string MegaCode(Form form) =>
        form.Kind == FormKind.Primal ? "0004"
        : form.Id.EndsWith("_mega_x", StringComparison.Ordinal) ? "0002"
        : form.Id.EndsWith("_mega_y", StringComparison.Ordinal) ? "0003"
        : "0001";

    private static void Missing(ImportedGameData data, IEnumerable<string> ids, string what)
    {
        var list = ids.ToList();
        if (list.Count > 0)
        {
            var shown = list.Count > 30 ? string.Join(", ", list.Take(30)) + $" and {list.Count - 30} more" : string.Join(", ", list);
            data.Warnings.Add($"{list.Count} {what} without a Portuguese name in the game text (the app shows the English name): {shown}");
        }
    }

    [GeneratedRegex(@"\(([^)]*)\)\s*$")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"[^A-Za-z0-9]+")]
    private static partial Regex Words();

    [GeneratedRegex(@"^(?:ALOLA|GALARIAN|HISUIAN|PALDEA)_")]
    private static partial Regex RegionPart();

    [GeneratedRegex(@"^(.+)_PLUS$")]
    private static partial Regex PlusMove();

    [GeneratedRegex(@"^HIDDEN_POWER_([A-Z]+)$")]
    private static partial Regex HiddenPower();

    [GeneratedRegex(@"^AEGISLASH_CHARGE_(.+)$")]
    private static partial Regex AegislashCharge();
}
