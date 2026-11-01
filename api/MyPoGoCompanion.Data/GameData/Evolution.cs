namespace MyPoGoCompanion.Data.GameData;

/// <summary>One evolution step (Gible → Gabite) and what it needs.</summary>
public class Evolution
{
    public int Id { get; set; }

    public required string FromFormId { get; set; }

    public Form FromForm { get; set; } = null!;

    public required string ToFormId { get; set; }

    public Form ToForm { get; set; } = null!;

    public int Candy { get; set; }

    /// <summary>Candy when the Pokémon was traded (0 for trade evolutions); null when trading doesn't change it.</summary>
    public int? CandyAfterTrade { get; set; }

    /// <summary>Evolution item (game id, e.g. <c>ITEM_SINNOH_STONE</c>).</summary>
    public string? ItemId { get; set; }

    public int? BuddyDistanceKm { get; set; }

    /// <summary><c>day</c> or <c>night</c>.</summary>
    public string? TimeOfDay { get; set; }

    /// <summary>Lure module needed (game id, e.g. <c>ITEM_TROY_DISK_MAGNETIC</c>).</summary>
    public string? LureId { get; set; }

    /// <summary><c>male</c> or <c>female</c>.</summary>
    public string? Gender { get; set; }

    /// <summary>A special task, in English (e.g. "Win 30 raids as buddy").</summary>
    public string? TaskEn { get; set; }

    public string? TaskPt { get; set; }
}
