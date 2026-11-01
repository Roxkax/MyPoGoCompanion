namespace MyPoGoCompanion.GoldenFiles;

/// <summary>Thrown by <see cref="GoldenAssert"/> when C# output differs from a golden file.</summary>
public sealed class GoldenFileMismatchException : Exception
{
    public GoldenFileMismatchException()
    {
    }

    public GoldenFileMismatchException(string message) : base(message)
    {
    }

    public GoldenFileMismatchException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>The differences found (at most <see cref="JsonCompareOptions.MaxDifferences"/>).</summary>
    public IReadOnlyList<JsonDifference> Differences { get; init; } = [];

    /// <summary>Where the C# output was written for comparing with a diff tool, if it was.</summary>
    public string? ActualPath { get; init; }
}
