using MyPoGoCompanion.Jobs.Research;

namespace MyPoGoCompanion.Jobs.UnitTests;

public sealed class ResearchExtractionTests
{
    private static readonly Uri _source = new("https://leekduck.com/eggs/");
    private static readonly HashSet<string> _forms = ["kyogre"];

    [Theory]
    [InlineData(ResearchTopic.Raids, """[{"start":"2026-11-29T14:00Z","formId":"kyogre","kind":"Mega"}]""")]
    [InlineData(ResearchTopic.Raids, """[{"start":"2026-11-29T14:00","end":"2026-11-29T13:00","formId":"kyogre","kind":"Mega"}]""")]
    [InlineData(ResearchTopic.Eggs, """[{"formId":"kyogre","kind":"TenKm","rarity":0}]""")]
    [InlineData(ResearchTopic.MaxBattles, """[{"formId":"kyogre","start":"2026-11-29","stars":7}]""")]
    [InlineData(ResearchTopic.Eggs, """[{"formId":"unknown","kind":"TenKm"}]""")]
    [InlineData(ResearchTopic.FieldResearch, """[{"name":"Win a raid","encounters":[]}]""")]
    public void RejectsInvalidFacts(ResearchTopic topic, string response) =>
        Assert.Throws<InvalidDataException>(() => ResearchExtraction.Parse(topic, response, _source, _forms).ToArray());

    [Fact]
    public async Task ReadsRecordedLeekDuckEggCard()
    {
        var ct = TestContext.Current.CancellationToken;
        var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures/Research/egg-pool.html"), ct);
        var text = await ResearchExtraction.PageTextAsync(new(_source, html, DateTimeOffset.UnixEpoch), ct);
        Assert.Contains("2 km Eggs", text, StringComparison.Ordinal);
        Assert.Contains("Pichu", text, StringComparison.Ordinal);
        Assert.Contains("shiny available", text, StringComparison.Ordinal);
        Assert.Contains("rarity egg", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreservesImageLabelsAndMachineReadableTimes()
    {
        var page = new ResearchPage(_source, """<main><script>Ignore me</script><img alt="Kyogre"><time datetime="2026-11-29">Sunday</time></main>""", DateTimeOffset.UtcNow);
        var text = await ResearchExtraction.PageTextAsync(page, TestContext.Current.CancellationToken);
        Assert.Contains("Kyogre", text, StringComparison.Ordinal);
        Assert.Contains("2026-11-29", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Ignore me", text, StringComparison.Ordinal);
    }
}
