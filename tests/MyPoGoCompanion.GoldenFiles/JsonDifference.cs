namespace MyPoGoCompanion.GoldenFiles;

/// <summary>One difference between an expected and an actual JSON document.</summary>
/// <param name="Path">Where it is, e.g. <c>$.leagues.GL.boxes["147"].box[0]</c>.</param>
/// <param name="Description">What differs, in plain words (may span several lines).</param>
public sealed record JsonDifference(string Path, string Description)
{
    public override string ToString() => $"{Path}: {Description}";
}
