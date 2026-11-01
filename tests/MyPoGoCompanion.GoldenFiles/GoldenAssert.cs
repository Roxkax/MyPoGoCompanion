using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyPoGoCompanion.GoldenFiles;

/// <summary>Compares C# output with a golden file and fails with a readable diff.</summary>
public static class GoldenAssert
{
    private static readonly JsonSerializerOptions _writeOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Fails unless <paramref name="actual"/> matches the golden output <paramref name="name"/>.
    /// On a mismatch the C# output is written to <paramref name="actualDirectory"/> (default:
    /// <c>TestResults/golden-actual/</c> at the repository root) so a diff tool can compare the two files.
    /// </summary>
    /// <exception cref="GoldenFileMismatchException">The output differs; the message lists where.</exception>
    public static void Matches(GoldenDirectory golden, string name, JsonNode? actual,
        JsonCompareOptions? options = null, string? actualDirectory = null)
    {
        var expectedPath = golden.Output(name);
        var comparison = JsonComparer.Compare(golden.ReadOutput(name), actual, options);
        if (comparison.AreEqual)
        {
            return;
        }

        actualDirectory ??= Path.Combine(GoldenDirectory.FindRepositoryRoot(golden.Root), "TestResults", "golden-actual");
        Directory.CreateDirectory(actualDirectory);
        var actualPath = Path.Combine(actualDirectory, name + ".json");
        File.WriteAllText(actualPath, (actual?.ToJsonString(_writeOptions) ?? "null") + "\n");

        throw new GoldenFileMismatchException(Describe(name, comparison, expectedPath, actualPath))
        {
            Differences = comparison.Differences,
            ActualPath = actualPath,
        };
    }

    /// <summary>Serialises <paramref name="actual"/> to JSON, then compares it like <see cref="Matches(GoldenDirectory, string, JsonNode?, JsonCompareOptions?, string?)"/>.</summary>
    public static void Matches<T>(GoldenDirectory golden, string name, T actual, JsonSerializerOptions serializerOptions,
        JsonCompareOptions? options = null, string? actualDirectory = null) =>
        Matches(golden, name, JsonSerializer.SerializeToNode(actual, serializerOptions), options, actualDirectory);

    private static string Describe(string name, JsonComparison comparison, string expectedPath, string actualPath)
    {
        var count = comparison.Differences.Count;
        var text = new StringBuilder()
            .Append("The C# output does not match the golden file ").Append(name).Append(".json: ")
            .Append(count).Append(comparison.Truncated ? "+ differences (showing the first " + count + ")" : count == 1 ? " difference" : " differences")
            .AppendLine();
        foreach (var difference in comparison.Differences)
        {
            text.Append("  ").AppendLine(difference.ToString());
        }

        return text
            .Append("Expected: ").AppendLine(expectedPath)
            .Append("Actual:   ").Append(actualPath)
            .ToString();
    }
}
