namespace MyPoGoCompanion.Data.GameData;

/// <summary>A move in a form's movepool.</summary>
public class FormMove
{
    public required string FormId { get; set; }

    public Form Form { get; set; } = null!;

    public required string MoveId { get; set; }

    public Move Move { get; set; } = null!;

    public MoveAvailability Availability { get; set; }
}
