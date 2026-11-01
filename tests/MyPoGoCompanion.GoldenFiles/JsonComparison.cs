namespace MyPoGoCompanion.GoldenFiles;

/// <summary>The result of <see cref="JsonComparer.Compare"/>.</summary>
/// <param name="Differences">The differences found, in document order.</param>
/// <param name="Truncated">Whether more differences exist than were listed.</param>
public sealed record JsonComparison(IReadOnlyList<JsonDifference> Differences, bool Truncated)
{
    public bool AreEqual => Differences.Count == 0;
}
