using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Api.Filters;

namespace MyPoGoCompanion.IntegrationTests;

public sealed class SavedFilterTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task FiltersPersistAcrossSessionsAndEveryOperationIsScopedToTheOwner()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await postgres.CreateDatabaseAsync(ct);
        var google = new FakeGoogle()
            .Add("misty-code", "misty@example.test", "Misty")
            .Add("brock-code", "brock@example.test", "Brock");
        await using var api = new ApiFactory(database, google);
        using var misty = api.CreateClient();
        using var otherDevice = api.CreateClient();
        using var brock = api.CreateClient();
        using var anonymous = api.CreateClient();

        async Task SignIn(HttpClient client, string code)
        {
            using var response = await client.PostAsJsonAsync("/auth/google", new
            {
                code,
                codeVerifier = new string('v', 64),
                redirectUri = ApiFactory.WebOrigin + "/auth/callback",
            }, ct);
            response.EnsureSuccessStatusCode();
            var session = (await response.Content.ReadFromJsonAsync<SessionResponse>(_json, ct))!;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        }

        await SignIn(misty, "misty-code");
        await SignIn(otherDevice, "misty-code");
        await SignIn(brock, "brock-code");

        const string listPath = "/saved-filters";
        const string search = "  +Gible;custom nickname&!#Raid team  ";
        using var unauthorized = await anonymous.PostAsJsonAsync(listPath, new SaveFilterRequest("A filter", search), ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var invalid = await misty.PostAsJsonAsync(listPath, new SaveFilterRequest("Bad search", "fire&"), ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var created = await misty.PostAsJsonAsync(listPath, new SaveFilterRequest("  My search  ", search), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        var original = (await created.Content.ReadFromJsonAsync<SavedFilterResponse>(ct))!;
        Assert.Equal(("My search", search), (original.Name, original.SearchString));
        var path = created.Headers.Location!;
        Assert.Equal($"/saved-filters/{original.Id}", path.OriginalString);
        Assert.Equal(original, await otherDevice.GetFromJsonAsync<SavedFilterResponse>(path, ct));
        Assert.Equal(original, Assert.Single((await otherDevice.GetFromJsonAsync<SavedFilterResponse[]>(listPath, ct))!));
        Assert.Empty((await brock.GetFromJsonAsync<SavedFilterResponse[]>(listPath, ct))!);

        using var foreignGet = await brock.GetAsync(path, ct);
        using var foreignRename = await brock.PutAsJsonAsync($"{path}/name", new NameFilterRequest("Intruder"), ct);
        using var foreignCopy = await brock.PostAsJsonAsync($"{path}/duplicate", new NameFilterRequest("Intruder"), ct);
        using var foreignDelete = await brock.DeleteAsync(path, ct);
        foreach (var response in new[] { foreignGet, foreignRename, foreignCopy, foreignDelete })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var renamed = await otherDevice.PutAsJsonAsync($"{path}/name", new NameFilterRequest("  Renamed  "), ct);
        Assert.Equal(HttpStatusCode.NoContent, renamed.StatusCode);
        var afterRename = (await misty.GetFromJsonAsync<SavedFilterResponse>(path, ct))!;
        Assert.Equal(("Renamed", search, original.CreatedAt), (afterRename.Name, afterRename.SearchString, afterRename.CreatedAt));

        const string legacyConditions = """{"conditions":[{"group":"pokedex","values":["443-445"]}]}""";
        await using var db = PostgresFixture.CreateContext(database);
        var stored = await db.SavedFilters.SingleAsync(ct);
        stored.Conditions = legacyConditions;
        await db.SaveChangesAsync(ct);

        using var duplicated = await misty.PostAsJsonAsync($"{path}/duplicate", new NameFilterRequest("My copy"), ct);
        Assert.Equal(HttpStatusCode.Created, duplicated.StatusCode);
        var copy = (await duplicated.Content.ReadFromJsonAsync<SavedFilterResponse>(ct))!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(search, copy.SearchString);
        Assert.NotNull((await db.SavedFilters.FindAsync([copy.Id], ct))?.Conditions);

        using var deleted = await misty.DeleteAsync(path, ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(copy, Assert.Single((await otherDevice.GetFromJsonAsync<SavedFilterResponse[]>(listPath, ct))!));
    }
}
