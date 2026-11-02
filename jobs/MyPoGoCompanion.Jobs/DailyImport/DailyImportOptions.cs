namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>Settings of the daily import (configuration section <c>DailyImport</c>, e.g. <c>DailyImport__Trigger</c>).</summary>
public sealed class DailyImportOptions
{
    public const string Section = "DailyImport";

    /// <summary>PvPoke's repository files (MIT): game master, rankings and the CP multiplier table.</summary>
    public Uri PvpokeBaseUrl { get; set; } = new("https://raw.githubusercontent.com/pvpoke/pvpoke/master/src/");

    /// <summary>The datamined game master (raid move numbers, evolutions, Dynamax).</summary>
    public Uri GameMasterUrl { get; set; } = new("https://raw.githubusercontent.com/alexelgt/game_masters/master/GAME_MASTER.json");

    /// <summary>
    /// The game's Brazilian Portuguese text from the latest release (Pokémon, move, type and form names), datamined by
    /// holoholo-text: <c>{"data": [key, text, key, text, …]}</c>.
    /// </summary>
    public Uri GameTextUrl { get; set; } = new("https://raw.githubusercontent.com/sora10pls/holoholo-text/main/Release/Brazilian%20Portuguese/pt-br_raw.json");

    /// <summary>The newer texts the game's server sends on top of the release (same format; they win).</summary>
    public Uri GameTextRemoteUrl { get; set; } = new("https://raw.githubusercontent.com/sora10pls/holoholo-text/main/Remote/Brazilian%20Portuguese/pt-br_raw.json");

    /// <summary>What started the run, kept in the run log: <c>schedule</c>, <c>deploy</c> or <c>manual</c>.</summary>
    public string Trigger { get; set; } = "manual";

    /// <summary>
    /// The import refuses sources that list fewer released forms than this share of the released forms already
    /// stored: a cut-off or broken file would otherwise mark most Pokémon as not released.
    /// </summary>
    public double MinimumReleasedFormsShare { get; set; } = 0.8;

    /// <summary>Attempts per file before the download counts as failed (GitHub's raw files sometimes answer 5xx).</summary>
    public int DownloadAttempts { get; set; } = 3;

    /// <summary>Wait before the second attempt; doubled before each later one.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Per file, including the 19 MB game master.</summary>
    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
