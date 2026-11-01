using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyPoGoCompanion.GoldenFiles.UnitTests;

public sealed class GoldenAssertTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("golden-").FullName;
    private readonly GoldenDirectory _golden;

    public GoldenAssertTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "outputs"));
        File.WriteAllText(Path.Combine(_root, "outputs", "strings.json"),
            """{"GL":{"cap":1500,"strings":[{"from":1,"to":485,"text":"1-485&3-4attack&cp-1500"}]}}""");
        _golden = new GoldenDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string ActualDirectory => Path.Combine(_root, "actual");

    private static JsonObject Strings(int to, string text) =>
        new JsonObject
        {
            ["GL"] = new JsonObject
            {
                ["cap"] = 1500,
                ["strings"] = new JsonArray(new JsonObject { ["from"] = 1, ["to"] = to, ["text"] = text }),
            },
        };

    [Fact]
    public void Matches_SameOutput_Passes()
    {
        GoldenAssert.Matches(_golden, "strings", Strings(485, "1-485&3-4attack&cp-1500"), actualDirectory: ActualDirectory);

        Assert.False(Directory.Exists(ActualDirectory));
    }

    [Fact]
    public void Matches_DifferentOutput_ThrowsWithAReadableDiffAndWritesTheActualFile()
    {
        var error = Assert.Throws<GoldenFileMismatchException>(() =>
            GoldenAssert.Matches(_golden, "strings", Strings(486, "1-486&3-4attack&cp-1500"), actualDirectory: ActualDirectory));

        var actualPath = Path.Combine(ActualDirectory, "strings.json");
        Assert.Equal(actualPath, error.ActualPath);
        Assert.Equal(2, error.Differences.Count);
        Assert.Equal(
            $"""
            The C# output does not match the golden file strings.json: 2 differences
              $.GL.strings[0].to: expected 485, found 486 (difference 1)
              $.GL.strings[0].text: expected "1-485&3-4attack&cp-1500", found "1-486&3-4attack&cp-1500"
            Expected: {_golden.Output("strings")}
            Actual:   {actualPath}
            """.ReplaceLineEndings(),
            error.Message);
        Assert.True(JsonComparer.Compare(Strings(486, "1-486&3-4attack&cp-1500"),
            JsonNode.Parse(File.ReadAllText(actualPath))).AreEqual);
    }

    [Fact]
    public void Matches_OneDifference_SaysDifference()
    {
        var error = Assert.Throws<GoldenFileMismatchException>(() =>
            GoldenAssert.Matches(_golden, "strings", Strings(486, "1-485&3-4attack&cp-1500"), actualDirectory: ActualDirectory));

        Assert.StartsWith("The C# output does not match the golden file strings.json: 1 difference" + Environment.NewLine, error.Message);
    }

    [Fact]
    public void Matches_MoreDifferencesThanShown_SaysSo()
    {
        var error = Assert.Throws<GoldenFileMismatchException>(() =>
            GoldenAssert.Matches(_golden, "strings", Strings(486, "x"), new JsonCompareOptions { MaxDifferences = 1 }, ActualDirectory));

        Assert.StartsWith("The C# output does not match the golden file strings.json: 1+ differences (showing the first 1)", error.Message);
    }

    [Fact]
    public void Matches_NullOutput_WritesNull()
    {
        var error = Assert.Throws<GoldenFileMismatchException>(() =>
            GoldenAssert.Matches(_golden, "strings", (JsonNode?)null, actualDirectory: ActualDirectory));

        Assert.Equal("null\n", File.ReadAllText(error.ActualPath!));
    }

    [Fact]
    public void Matches_WithoutAnActualDirectory_WritesUnderTestResultsAtTheRepositoryRoot()
    {
        File.WriteAllText(Path.Combine(_root, GoldenDirectory.SolutionFile), "<Solution />");

        var error = Assert.Throws<GoldenFileMismatchException>(() => GoldenAssert.Matches(_golden, "strings", new JsonObject()));

        Assert.Equal(Path.Combine(_root, "TestResults", "golden-actual", "strings.json"), error.ActualPath);
    }

    private sealed record StringRow(int From, int To, string Text);

    private sealed record League(int Cap, StringRow[] Strings);

    [Fact]
    public void Matches_TypedOutput_IsSerialisedWithTheGivenOptions()
    {
        var output = new Dictionary<string, League> { ["GL"] = new(1500, [new StringRow(1, 485, "1-485&3-4attack&cp-1500")]) };

        GoldenAssert.Matches(_golden, "strings", output, new JsonSerializerOptions(JsonSerializerDefaults.Web), actualDirectory: ActualDirectory);
    }

    [Fact]
    public void Exception_StandardConstructors_KeepTheMessage()
    {
        Assert.Empty(new GoldenFileMismatchException().Differences);
        Assert.Equal("m", new GoldenFileMismatchException("m").Message);
        var inner = new InvalidOperationException();
        Assert.Same(inner, new GoldenFileMismatchException("m", inner).InnerException);
    }
}
