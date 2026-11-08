namespace MyPoGoCompanion.Data.GameData;

/// <summary>
/// An event costume of a species (decision 2026-11-08): known from PokeMiners' costume pictures
/// (<c>pm25.cFALL_2018.icon.png</c>, or <c>pm710.fSMALL.cFALL_2022.icon.png</c> for a form's costume). No source
/// says when a costume was out or whether it's retired, so there are no dates.
/// </summary>
public class Costume
{
    /// <summary>The picture's name without <c>.icon.png</c> (<c>pm710.fSMALL.cFALL_2022</c>); the key.</summary>
    public required string Sprite { get; set; }

    public int Dex { get; set; }

    public Species Species { get; set; } = null!;

    /// <summary>The costume's code (<c>FALL_2022</c>, <c>JAN_2020_NOEVOLVE</c>).</summary>
    public required string Code { get; set; }

    /// <summary>The form the costume is on (<c>SMALL</c>, <c>GALARIAN</c>); null for the plain form.</summary>
    public string? FormCode { get; set; }

    /// <summary>A readable name made from the codes ("Fall 2022 (Small)").</summary>
    public required string NameEn { get; set; }

    /// <summary>PokeMiners has its shiny picture (<c>.s</c>); it says nothing about whether the shiny is out.</summary>
    public bool HasShinySprite { get; set; }
}
