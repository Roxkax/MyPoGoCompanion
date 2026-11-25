using System.Globalization;
using MyPoGoCompanion.Engine.Search;
using static MyPoGoCompanion.Engine.Search.BuilderSearch.SearchCheckStatus;

namespace MyPoGoCompanion.Engine.UnitTests;

public sealed class BuilderSearchTests
{
    [Theory]
    [InlineData("443-445&Age0&3-4Attack&!shiny")]
    [InlineData("fire;water|!lucky&4*&cp-1500&hp100-")]
    [InlineData("distance 1000-1200&year2016&@3ghost&buddy2-5&!#Raid team")]
    [InlineData("mega0-,gigantamax&maxmove1-&ultra beasts")]
    public void KnownSyntaxKeepsTheOriginalString(string text)
    {
        var result = BuilderSearch.Check(text);
        Assert.Equal(Valid, result.Status);
        Assert.Equal(text, Assert.Single(result.Parts));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("shiny&&lucky")]
    [InlineData("shiny,")]
    [InlineData("!")]
    [InlineData("! shiny")]
    [InlineData("!!shiny")]
    [InlineData("(shiny,lucky)&fire")]
    [InlineData("cp1500-1000")]
    [InlineData("cp 1500")]
    [InlineData("cp3attack")]
    [InlineData("0")]
    [InlineData("buddy6")]
    [InlineData("5attack")]
    [InlineData("0-5*")]
    [InlineData("hp2147483648")]
    public void MalformedSearchDoesNotProduceCopyableParts(string? text)
    {
        var result = BuilderSearch.Check(text);
        Assert.Equal(Invalid, result.Status);
        Assert.Empty(result.Parts);
    }

    [Fact]
    public void ANameWithoutCatalogDataStaysCopyableButUnverified()
    {
        const string text = "fire,+gible&@earth power";
        var unknown = BuilderSearch.Check(text);
        Assert.Equal(Unverified, unknown.Status);
        Assert.Equal("+gible", unknown.Term);
        Assert.Equal(text, Assert.Single(unknown.Parts));

        var known = BuilderSearch.Check(text, term => term is "+gible" or "@earth power");
        Assert.Equal(Valid, known.Status);
    }

    [Fact]
    public void EachPartKeepsAllAndConditionsAndTheirUnionMatchesTheOriginal()
    {
        var dexes = string.Join(',', Enumerable.Range(1, 950).Select(dex => dex.ToString(CultureInfo.InvariantCulture)));
        var expression = $"{dexes}&3-4attack&cp-1500";
        var result = BuilderSearch.Check(expression);

        Assert.Equal(Valid, result.Status);
        Assert.True(result.Parts.Count > 1);
        Assert.All(result.Parts, part =>
        {
            Assert.True(part.Length <= BuilderSearch.CharacterLimit);
            Assert.EndsWith("&3-4attack&cp-1500", part, StringComparison.Ordinal);
        });

        var split = result.Parts.Select(part => GameSearch.Parse(part)).ToArray();
        var original = GameSearch.Parse(expression);
        foreach (var dex in Enumerable.Range(1, 1000))
        {
            foreach (var subject in new[] { new SearchSubject(dex, 3, 0, 0, 1499), new(dex, 1, 0, 0, 1499), new(dex, 4, 0, 0, 1501) })
            {
                Assert.Equal(original.Matches(subject), split.Any(part => part.Matches(subject)));
            }
        }
    }

    [Fact]
    public void TooManyRequiredTermsOrAnOversizedSingleValueCannotBeSplit()
    {
        Assert.Equal(CannotSplit, BuilderSearch.Check(string.Join('&', Enumerable.Repeat("!shiny", 400))).Status);
        Assert.Equal(CannotSplit, BuilderSearch.Check($"fire,#{new string('a', 1950)}").Status);
        Assert.Equal(TooLong, BuilderSearch.Check(new string('x', BuilderSearch.InputLimit + 1)).Status);
    }
}
