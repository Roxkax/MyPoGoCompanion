using System.Text.RegularExpressions;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Api.Pokedex;

/// <summary>
/// The names the Pokédex list shows for a version: its marker's short text ("Mega Y", "Hisuian", "Origin") and the
/// name after "Showing …" ("Mega Charizard Y", "Hisuian Arcanine"). English names come from PvPoke
/// ("Charizard (Mega Y)"), Portuguese ones from the game's text ("Mega Charizard Y", "Arcanine de Hisui",
/// "Giratina (Forma Origem)", "Kyurem Branco").
/// </summary>
internal static partial class PokedexNames
{
    /// <summary>The text in brackets of an English form name ("Mega Y" in "Charizard (Mega Y)"); null for the main form.</summary>
    public static string? LabelEn(string formName, bool isDefault)
    {
        if (isDefault)
        {
            return null;
        }

        var match = Bracket().Match(formName);
        return match.Success ? match.Groups["inner"].Value.Trim() : null;
    }

    /// <summary>
    /// The English name to show: "Mega Charizard Y", "Primal Kyogre", "Alolan Vulpix", "Galarian Darmanitan (Zen)";
    /// other forms keep PvPoke's name ("Giratina (Origin)").
    /// </summary>
    public static string DisplayNameEn(string formName, string speciesName, FormKind kind, bool isDefault)
    {
        if (LabelEn(formName, isDefault) is not { } label)
        {
            return formName;
        }

        switch (kind)
        {
            case FormKind.Mega or FormKind.Primal:
                // "Mega X" → "Mega Charizard X"; "Mega" → "Mega Venusaur"; "Primal" → "Primal Kyogre".
                var space = label.IndexOf(' ', StringComparison.Ordinal);
                return space < 0 ? $"{label} {speciesName}" : $"{label[..space]} {speciesName}{label[space..]}";
            case FormKind.Regional:
                // "Alolan" → "Alolan Vulpix"; "Galarian Zen" → "Galarian Darmanitan (Zen)".
                var rest = label.IndexOf(' ', StringComparison.Ordinal);
                return rest < 0 ? $"{label} {speciesName}" : $"{label[..rest]} {speciesName} ({label[(rest + 1)..]})";
            default:
                return formName;
        }
    }

    /// <summary>
    /// The marker's text in Portuguese, from the game's name: the text in brackets ("Forma Origem"), else the name
    /// without the species' name ("Mega Y", "Branco", "Primitivo") and without a leading "de" ("Alola"). Null for the
    /// main form, or when the game text has no name.
    /// </summary>
    public static string? LabelPt(string? formName, string? speciesName, bool isDefault)
    {
        if (isDefault || string.IsNullOrWhiteSpace(formName))
        {
            return null;
        }

        var match = Bracket().Match(formName);
        if (match.Success)
        {
            return match.Groups["inner"].Value.Trim();
        }

        var label = string.IsNullOrWhiteSpace(speciesName) ? formName : formName.Replace(speciesName, " ", StringComparison.Ordinal);
        label = Spaces().Replace(label, " ").Trim();
        if (label.StartsWith("de ", StringComparison.Ordinal))
        {
            label = label[3..];
        }

        return label.Length == 0 || label == formName ? null : label;
    }

    [GeneratedRegex(@"\((?<inner>[^()]+)\)\s*$")]
    private static partial Regex Bracket();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
