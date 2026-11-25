using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyPoGoCompanion.Api.Filters;
using MyPoGoCompanion.Data.Seed;
using MyPoGoCompanion.Engine.Search;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class FilterBuilderTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerOptions.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task PublishedSpeciesAndMovesAreRecognizedWithoutAnAccount()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await postgres.CreateDatabaseAsync(ct);
        await using var db = PostgresFixture.CreateContext(database);
        await DevSeed.SeedAsync(db, DateTimeOffset.UtcNow, ct);
        await using var api = new ApiFactory(database);
        using var client = api.CreateClient();

        async Task<BuilderSearch.SearchCheckResult> CheckAsync(string expression)
        {
            using var response = await client.PostAsJsonAsync("/filter-search/check", new FilterBuilderRequest(expression), ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<BuilderSearch.SearchCheckResult>(_json, ct))!;
        }

        const string known = "+Gible&@Earth Power&!#Raid";
        var result = await CheckAsync(known);
        Assert.Equal(BuilderSearch.SearchCheckStatus.Valid, result.Status);
        Assert.Equal(known, Assert.Single(result.Parts));

        var unknown = await CheckAsync("fire&not-in-the-catalog");
        Assert.Equal(BuilderSearch.SearchCheckStatus.Unverified, unknown.Status);
        Assert.Equal("not-in-the-catalog", unknown.Term);

        var invalid = await CheckAsync("fire&");
        Assert.Equal(BuilderSearch.SearchCheckStatus.Invalid, invalid.Status);
        Assert.Empty(invalid.Parts);
    }
}
