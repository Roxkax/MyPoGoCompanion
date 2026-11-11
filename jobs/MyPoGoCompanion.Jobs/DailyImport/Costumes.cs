using System.Globalization;
using System.Text.RegularExpressions;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// Event costumes from PokeMiners' costume pictures (decision 2026-11-08): neither PvPoke nor the game master says
/// which costumes were out, so the picture folder's file list is the source. A costume picture is
/// <c>pm{dex}[.f{FORM}].c{COSTUME}[.g2][.s].icon.png</c>; female variants (<c>.g2</c>) are left out, a shiny picture
/// (<c>.s</c>) only marks that one exists.
/// </summary>
public static partial class Costumes
{
    /// <summary>The costumes in a list of PokeMiners' file names, for the given Pokédex numbers, in name order.</summary>
    public static IReadOnlyList<Costume> FromFileNames(IEnumerable<string> fileNames, IReadOnlySet<int> dexes)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentNullException.ThrowIfNull(dexes);
        var found = new SortedDictionary<string, Costume>(StringComparer.Ordinal);
        foreach (var name in fileNames)
        {
            var match = FileName().Match(name);
            if (!match.Success || match.Groups["female"].Success
                || !int.TryParse(match.Groups["dex"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var dex)
                || !dexes.Contains(dex))
            {
                continue;
            }

            var formCode = match.Groups["form"].Success ? match.Groups["form"].Value : null;
            var code = match.Groups["costume"].Value;
            var sprite = $"pm{dex.ToString(CultureInfo.InvariantCulture)}{(formCode is null ? "" : ".f" + formCode)}.c{code}";
            if (!found.TryGetValue(sprite, out var costume))
            {
                costume = new Costume { Sprite = sprite, Dex = dex, Code = code, FormCode = formCode, NameEn = Name(code, formCode) };
                found.Add(sprite, costume);
            }

            costume.HasShinySprite |= match.Groups["shiny"].Success;
        }

        return [.. found.Values];
    }

    /// <summary>Adds the costumes of the import's species from the file list.</summary>
    public static void Add(ImportedGameData data, IEnumerable<string> fileNames)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.Costumes.AddRange(FromFileNames(fileNames, data.Species.Select(species => species.Dex).ToHashSet()));
    }

    /// <summary>A readable name: <c>FALL_2022</c> + <c>SMALL</c> → "Fall 2022 (Small)"; "no evolve" markers dropped.</summary>
    public static string Name(string code, string? formCode = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        var words = code.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !word.Equals("NOEVOLVE", StringComparison.OrdinalIgnoreCase))
            .Select(Word);
        var name = string.Join(' ', words);
        if (name.Length == 0)
        {
            name = "Costume";
        }

        return formCode is null ? name : $"{name} ({string.Join(' ', formCode.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(Word))})";
    }

    private static string Word(string word) => word.ToUpperInvariant() switch
    {
        "GOFEST" => "GO Fest",
        "GO" => "GO",
        "JAN" => "January",
        "FEB" => "February",
        "MAR" => "March",
        "APR" => "April",
        "JUN" => "June",
        "JUL" => "July",
        "AUG" => "August",
        "SEP" or "SEPT" => "September",
        "OCT" => "October",
        "NOV" => "November",
        "DEC" => "December",
        var upper => char.ToUpperInvariant(upper[0]) + upper[1..].ToLowerInvariant(),
    };

    [GeneratedRegex(@"^pm(?<dex>\d+)(?:\.f(?<form>[A-Z0-9_]+))?\.c(?<costume>[A-Za-z0-9_]+)(?<female>\.g2)?(?<shiny>\.s)?\.icon\.png$", RegexOptions.CultureInvariant)]
    private static partial Regex FileName();
}
