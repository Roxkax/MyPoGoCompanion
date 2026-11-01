namespace MyPoGoCompanion.GoldenFiles;

/// <summary>How <see cref="JsonComparer"/> compares two JSON documents.</summary>
public sealed record JsonCompareOptions
{
    /// <summary>Exact comparison: every number, string and boolean must be equal.</summary>
    public static JsonCompareOptions Exact { get; } = new();

    /// <summary>
    /// Largest relative difference allowed between two numbers (0 = exact). Integers and
    /// floating-point numbers always compare by value, so <c>40</c> equals <c>40.0</c>.
    /// </summary>
    public double RelativeTolerance { get; init; }

    /// <summary>Stop after this many differences; the count says when there were more.</summary>
    public int MaxDifferences { get; init; } = 20;
}
