using System.Text.Json.Nodes;

namespace MyPoGoCompanion.GoldenFiles.UnitTests;

public class JsonComparerTests
{
    private static JsonComparison Compare(string expected, string actual, JsonCompareOptions? options = null) =>
        JsonComparer.Compare(JsonNode.Parse(expected), JsonNode.Parse(actual), options);

    private static string Only(JsonComparison comparison) => Assert.Single(comparison.Differences).ToString();

    [Fact]
    public void Compare_SameDocument_IsEqual()
    {
        const string json = """{"a":[1,2.5,"x",true,false,null],"b":{"c":{}},"d":[]}""";

        var comparison = Compare(json, json);

        Assert.True(comparison.AreEqual);
        Assert.False(comparison.Truncated);
    }

    [Fact]
    public void Compare_PropertyOrder_IsIgnored() => Assert.True(Compare("""{"a":1,"b":2}""", """{"b":2,"a":1}""").AreEqual);

    [Fact]
    public void Compare_IntegerAndFloatWithSameValue_AreEqual() => Assert.True(Compare("[40.0, 1e2]", "[40, 100]").AreEqual);

    [Fact]
    public void Compare_DifferentNumber_ShowsBothAndTheDifference() =>
        Assert.Equal("$.leagues.GL.cap: expected 1500, found 1501 (difference 1)",
            Only(Compare("""{"leagues":{"GL":{"cap":1500}}}""", """{"leagues":{"GL":{"cap":1501}}}""")));

    [Fact]
    public void Compare_WithinRelativeTolerance_IsEqual()
    {
        var options = new JsonCompareOptions { RelativeTolerance = 1e-12 };

        Assert.True(Compare("[47.649965308638]", "[47.64996530863801]", options).AreEqual);
        Assert.False(Compare("[47.649965308638]", "[47.6499653087]", options).AreEqual);
    }

    [Fact]
    public void Compare_ExactByDefault_FlagsTheLastDigit() =>
        Assert.False(Compare("[47.649965308638]", "[47.64996530863801]").AreEqual);

    [Fact]
    public void Compare_MissingAndUnexpectedProperties_AreListed()
    {
        var comparison = Compare("""{"a":1,"b":2}""", """{"a":1,"c":3}""");

        Assert.Equal(
            ["$: missing property \"b\" (expected 2)", "$: unexpected property \"c\" = 3"],
            comparison.Differences.Select(d => d.ToString()));
    }

    [Fact]
    public void Compare_ArrayLength_IsReportedAndCommonItemsCompared()
    {
        var comparison = Compare("[1,2,3]", "[1,5]");

        Assert.Equal(["$: 3 items expected, 2 found", "$[1]: expected 2, found 5 (difference 3)"],
            comparison.Differences.Select(d => d.ToString()));
    }

    [Theory]
    [InlineData("""{"x":1}""", """{"x":"1"}""", "$.x: expected 1, found \"1\"")]
    [InlineData("""{"x":[1]}""", """{"x":{"0":1}}""", "$.x: expected array [1], found object {\"0\":1}")]
    [InlineData("""{"x":null}""", """{"x":0}""", "$.x: expected null, found 0")]
    [InlineData("""{"x":true}""", """{"x":false}""", "$.x: expected true, found false")]
    [InlineData("""{"x":"a"}""", """{"x":"b"}""", "$.x: expected \"a\", found \"b\"")]
    public void Compare_DifferentValues_DescribesBoth(string expected, string actual, string difference) =>
        Assert.Equal(difference, Only(Compare(expected, actual)));

    [Fact]
    public void Compare_PropertyNamesThatAreNotIdentifiers_UseBrackets() =>
        Assert.Equal("$.raid[\"11-15\"].boxes[\"147\"][0]: expected 3, found 4 (difference 1)",
            Only(Compare("""{"raid":{"11-15":{"boxes":{"147":[3]}}}}""",
                """{"raid":{"11-15":{"boxes":{"147":[4]}}}}""")));

    [Fact]
    public void Compare_LongStrings_ShowTheFirstDifferenceInContext()
    {
        var prefix = string.Concat(Enumerable.Repeat("1-9,", 30));
        var expected = new JsonArray(prefix + "13-18&3-4attack&cp-1500");
        var actual = new JsonArray(prefix + "13-19&3-4attack&cp-1500");

        var difference = Assert.Single(JsonComparer.Compare(expected, actual).Differences);

        Assert.Equal("$[0]", difference.Path);
        var lines = difference.Description.Split(Environment.NewLine);
        Assert.Equal("strings differ at character 124 (lengths 143 and 143)", lines[0]);
        Assert.Equal("    expected …\"9,1-9,1-9,1-9,1-9,1-9,1-9,13-18&3-4attack&cp-1500\"", lines[1]);
        Assert.Equal("    found    …\"9,1-9,1-9,1-9,1-9,1-9,1-9,13-19&3-4attack&cp-1500\"", lines[2]);
    }

    [Fact]
    public void Compare_LongStringThatIsAPrefix_PointsAtItsEnd()
    {
        var text = new string('a', 100);

        var difference = Assert.Single(JsonComparer.Compare(new JsonArray(text), new JsonArray(text + "bc")).Differences);

        Assert.StartsWith("strings differ at character 100 (lengths 100 and 102)", difference.Description);
        Assert.EndsWith("aaa\"", difference.Description.Split(Environment.NewLine)[1]);
        Assert.EndsWith("aaabc\"", difference.Description.Split(Environment.NewLine)[2]);
    }

    [Fact]
    public void Compare_LongValues_AreShortened()
    {
        var big = "[" + string.Join(",", Enumerable.Range(0, 100)) + "]";

        var difference = Only(Compare($$"""{"a":{{big}}}""", "{}"));

        Assert.StartsWith("$: missing property \"a\" (expected array [0,1,2,", difference);
        Assert.EndsWith("…)", difference);
    }

    [Fact]
    public void Compare_ManyDifferences_StopsAtTheMaximum()
    {
        var options = new JsonCompareOptions { MaxDifferences = 3 };

        var comparison = Compare("[1,2,3,4,5]", "[0,0,0,0,0]", options);

        Assert.Equal(3, comparison.Differences.Count);
        Assert.True(comparison.Truncated);
    }

    [Theory]
    [InlineData("""{"a":1,"b":2}""", """{"c":3}""")]
    [InlineData("""{"a":1}""", """{"b":2,"c":3}""")]
    [InlineData("""{"a":{"x":1},"b":{"x":1}}""", """{"a":{"x":2},"b":{"x":2}}""")]
    [InlineData("""[[1],[1]]""", """[[2],[2]]""")]
    [InlineData("""[1,2]""", """[3]""")]
    public void Compare_MaximumReachedInAnyBranch_IsTruncated(string expected, string actual)
    {
        var comparison = Compare(expected, actual, new JsonCompareOptions { MaxDifferences = 1 });

        Assert.Single(comparison.Differences);
        Assert.True(comparison.Truncated);
    }

    [Fact]
    public void Compare_BothNull_IsEqual() => Assert.True(JsonComparer.Compare(null, null).AreEqual);

    [Fact]
    public void Compare_NodesBuiltInCode_CompareByValue()
    {
        var expected = JsonNode.Parse("""{"level":40.0,"top":true,"name":"Mewtwo"}""");
        var actual = new JsonObject { ["level"] = 40, ["top"] = true, ["name"] = "Mewtwo" };

        Assert.True(JsonComparer.Compare(expected, actual).AreEqual);
    }
}
