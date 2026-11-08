using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using MyPoGoCompanion.Data.GameData;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>What a check of one picture in PokeMiners' folder found.</summary>
public enum SpriteCheck
{
    Exists,
    Missing,

    /// <summary>No clear answer (GitHub refused or didn't answer): the import keeps the pictures it had.</summary>
    Unknown,
}

/// <summary>A form's pictures as the database has them.</summary>
public sealed record StoredSprites(string? Sprite, string? Shiny, string? Gigantamax);

/// <summary>
/// The names PokeMiners gives a form's picture in <c>Images/Pokemon - 256x256/Addressable Assets</c> (decision
/// 2026-11-07, docs/SPRITES.md): <c>pm{dex}</c> for a plain form, <c>pm{dex}.f{FORM}</c> for the others, with
/// <c>.s</c> before <c>.icon.png</c> for the shiny one. The names aren't consistent (Kyurem's plain picture is
/// <c>pm646.fNORMAL</c>, Unown's letters <c>pm201.fUNOWN_A</c>), so each form gets a short list to try in order.
/// </summary>
public static partial class SpriteNames
{
    /// <summary>The file name's ending after the picture's name.</summary>
    public const string FileSuffix = ".icon.png";

    public static string Shiny(string sprite) => sprite + ".s";

    public static string Gigantamax(int dex) => $"pm{dex.ToString(CultureInfo.InvariantCulture)}.fGIGANTAMAX";

    /// <summary>
    /// The names to try for a form: a Mega or Primal by its PvPoke id (<c>charizard_mega_x</c> → <c>pm6.fMEGA_X</c>);
    /// another form by its game master template (<c>VULPIX_ALOLA</c> → <c>pm37.fALOLA</c>, then <c>pm37.fVULPIX_ALOLA</c>;
    /// a template without a form or with <c>_NORMAL</c> → <c>pm37</c>, then <c>pm37.fNORMAL</c>). A form the game master
    /// doesn't have gets the plain name when it is the main form, else nothing (it shows its main form's picture).
    /// A plain form also tries the pictures of <paramref name="lookalikes"/>, the game master's other forms with the
    /// same stats and types (Toxtricity → <c>pm849.fAMPED</c>, Unown → <c>pm201.fUNOWN_F</c>): PokeMiners has no plain
    /// picture for those species.
    /// </summary>
    public static IReadOnlyList<string> Candidates(
        string formId, int dex, bool isMega, bool isDefault, GameMasterPokemon? template, IEnumerable<GameMasterPokemon>? lookalikes = null)
    {
        ArgumentNullException.ThrowIfNull(formId);
        var plain = "pm" + dex.ToString(CultureInfo.InvariantCulture);
        if (isMega)
        {
            return MegaSuffix().Match(formId) is { Success: true } mega
                ? [$"{plain}.f{(mega.Groups[1].Value + mega.Groups[2].Value).ToUpperInvariant()}"]
                : [];
        }

        if (template is null)
        {
            return isDefault ? [plain, plain + ".fNORMAL"] : [];
        }

        var own = Named(plain, template);
        if (own is not null)
        {
            return own;
        }

        List<string> names = [plain, plain + ".fNORMAL"];
        foreach (var other in lookalikes ?? [])
        {
            names.AddRange((Named(plain, other) ?? []).Except(names, StringComparer.Ordinal));
        }

        return names;
    }

    /// <summary>A form template's own names, or null for a template without a form or with <c>_NORMAL</c>.</summary>
    private static string[]? Named(string plain, GameMasterPokemon template)
    {
        var form = template.Form;
        var suffix = form is null ? null
            : form.StartsWith(template.PokemonId + "_", StringComparison.Ordinal) ? form[(template.PokemonId.Length + 1)..]
            : form;
        return suffix is null or "NORMAL" ? null : [$"{plain}.f{suffix}", $"{plain}.f{form}"];
    }

    [GeneratedRegex(@"_(mega|primal)(_x|_y)?$")]
    private static partial Regex MegaSuffix();
}

/// <summary>
/// Gives every form its pictures (docs/SPRITES.md): its own when PokeMiners has one, else its main form's (a Mega's
/// base form, otherwise the species' main form), else none. A picture found once is trusted on later runs; the
/// others are checked again each time the import runs. When GitHub doesn't answer clearly, a form keeps the pictures
/// it had. Everything it couldn't find goes to the run log's warnings.
/// </summary>
public static class SpriteResolver
{
    public static async Task ResolveAsync(
        ImportedGameData data,
        IReadOnlyDictionary<string, StoredSprites> stored,
        Func<string, CancellationToken, Task<SpriteCheck>> check,
        int parallelism,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(check);

        var own = new ConcurrentDictionary<string, Own>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(
            data.Forms,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, parallelism), CancellationToken = cancellationToken },
            async (form, ct) => own[form.Id] = await FindOwnAsync(form, data.SpriteCandidates.GetValueOrDefault(form.Id, []),
                stored.GetValueOrDefault(form.Id), check, ct));

        var defaults = data.Forms.Where(form => form.IsDefault).ToDictionary(form => form.Dex, form => form.Id);
        List<string> borrowed = [], none = [], noneUnreleased = [], noGigantamax = [], notChecked = [];
        foreach (var form in data.Forms.OrderBy(form => form.Dex).ThenBy(form => form.Id, StringComparer.Ordinal))
        {
            var mine = own[form.Id];
            if (mine.Unknown)
            {
                var kept = stored.GetValueOrDefault(form.Id);
                (form.Sprite, form.SpriteShiny, form.SpriteGigantamax) = (kept?.Sprite, kept?.Shiny, kept?.Gigantamax);
                notChecked.Add(form.Id);
                continue;
            }

            form.SpriteGigantamax = mine.Gigantamax;
            if (form.CanGigantamax && mine.Gigantamax is null && form.Released)
            {
                noGigantamax.Add(form.Id);
            }

            if (mine.Sprite is not null)
            {
                (form.Sprite, form.SpriteShiny) = (mine.Sprite, mine.Shiny);
                continue;
            }

            var mainId = form.BaseFormId ?? defaults.GetValueOrDefault(form.Dex);
            var main = mainId is not null && mainId != form.Id ? own.GetValueOrDefault(mainId) : null;
            if (main is { Unknown: false, Sprite: not null })
            {
                (form.Sprite, form.SpriteShiny) = (main.Sprite, main.Shiny);
                if (form.Released)
                {
                    borrowed.Add(form.Id);
                }
            }
            else if (main is { Unknown: true } && stored.GetValueOrDefault(mainId!) is { } mainKept)
            {
                (form.Sprite, form.SpriteShiny) = (mainKept.Sprite, mainKept.Shiny);
            }
            else
            {
                (form.Sprite, form.SpriteShiny) = (null, null);
                (form.Released ? none : noneUnreleased).Add(form.Id);
            }
        }

        Warn(data, borrowed, "released forms show their main form's picture (PokeMiners has none of their own)");
        Warn(data, none, "released forms have no picture (the app shows a placeholder)");
        Warn(data, noneUnreleased, "forms not released in the game have no picture yet");
        Warn(data, noGigantamax, "released forms that can Gigantamax have no G-Max picture (the app shows the normal one)");
        if (notChecked.Count > 0)
        {
            data.Warnings.Add($"Couldn't check every picture (GitHub didn't answer clearly): {notChecked.Count} forms keep the pictures they had; the next import checks them again.");
        }
    }

    private static async Task<Own> FindOwnAsync(
        Form form, IReadOnlyList<string> candidates, StoredSprites? stored, Func<string, CancellationToken, Task<SpriteCheck>> check,
        CancellationToken cancellationToken)
    {
        string? sprite = null;
        if (stored?.Sprite is { } known && candidates.Contains(known, StringComparer.Ordinal))
        {
            sprite = known;
        }
        else
        {
            foreach (var candidate in candidates)
            {
                var found = await check(candidate, cancellationToken);
                if (found == SpriteCheck.Unknown)
                {
                    return Own.NotKnown;
                }

                if (found == SpriteCheck.Exists)
                {
                    sprite = candidate;
                    break;
                }
            }
        }

        string? shiny = null;
        if (sprite is not null)
        {
            var name = SpriteNames.Shiny(sprite);
            var found = stored?.Shiny == name ? SpriteCheck.Exists : await check(name, cancellationToken);
            if (found == SpriteCheck.Unknown)
            {
                return Own.NotKnown;
            }

            shiny = found == SpriteCheck.Exists ? name : null;
        }

        string? gigantamax = null;
        if (form.CanGigantamax)
        {
            var name = SpriteNames.Gigantamax(form.Dex);
            var found = stored?.Gigantamax == name ? SpriteCheck.Exists : await check(name, cancellationToken);
            if (found == SpriteCheck.Unknown)
            {
                return Own.NotKnown;
            }

            gigantamax = found == SpriteCheck.Exists ? name : null;
        }

        return new Own(sprite, shiny, gigantamax, Unknown: false);
    }

    private static void Warn(ImportedGameData data, List<string> ids, string what)
    {
        if (ids.Count > 0)
        {
            var shown = ids.Count > 20 ? string.Join(", ", ids.Take(20)) + $" and {ids.Count - 20} more" : string.Join(", ", ids);
            data.Warnings.Add($"{ids.Count} {what}: {shown}");
        }
    }

    private sealed record Own(string? Sprite, string? Shiny, string? Gigantamax, bool Unknown)
    {
        public static readonly Own NotKnown = new(null, null, null, Unknown: true);
    }
}
