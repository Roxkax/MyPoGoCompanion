using System.Net;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>The test copies of the source files (Fixtures/make_fixtures.py): real data for 28 species.</summary>
internal static class Fixtures
{
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static byte[] Bytes(string name) => System.IO.File.ReadAllBytes(Path.Combine(Folder, name));

    public static SourceFile File(string name, string label) => new(label, new Uri("https://example.test/" + name), Bytes(name));

    public static SourceFiles Sources() => new(
        File("pvpoke-gamemaster.json", "PvPoke game master"),
        File("pvpoke-Pokemon.js", "PvPoke CP multipliers"),
        File("pvpoke-rankings-1500.json", "PvPoke Great League rankings"),
        File("pvpoke-rankings-2500.json", "PvPoke Ultra League rankings"),
        File("pvpoke-rankings-10000.json", "PvPoke Master League rankings"),
        File("GAME_MASTER.json", "Game master"));

    private static ImportedGameData? _built;

    /// <summary>The fixtures built once (the builder doesn't change its input).</summary>
    public static ImportedGameData Built => _built ??= GameDataBuilder.Build(Sources());

    /// <summary>Answers each source address with its fixture file, as raw.githubusercontent.com would.</summary>
    public static Dictionary<string, Func<HttpResponseMessage>> Responses(Uri pvpokeBase, Uri gameMaster) => new()
    {
        [new Uri(pvpokeBase, "data/gamemaster.json").AbsoluteUri] = () => Ok("pvpoke-gamemaster.json"),
        [new Uri(pvpokeBase, "js/pokemon/Pokemon.js").AbsoluteUri] = () => Ok("pvpoke-Pokemon.js"),
        [new Uri(pvpokeBase, "data/rankings/all/overall/rankings-1500.json").AbsoluteUri] = () => Ok("pvpoke-rankings-1500.json"),
        [new Uri(pvpokeBase, "data/rankings/all/overall/rankings-2500.json").AbsoluteUri] = () => Ok("pvpoke-rankings-2500.json"),
        [new Uri(pvpokeBase, "data/rankings/all/overall/rankings-10000.json").AbsoluteUri] = () => Ok("pvpoke-rankings-10000.json"),
        [gameMaster.AbsoluteUri] = () => Ok("GAME_MASTER.json"),
    };

    public static HttpResponseMessage Ok(string name) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes(name)) };
}

/// <summary>An HTTP handler that answers from a table and counts the requests.</summary>
internal sealed class FakeHttp(Dictionary<string, Func<HttpResponseMessage>> responses) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public Dictionary<string, Func<HttpResponseMessage>> Responses { get; } = responses;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add(url);
        return Task.FromResult(Responses.TryGetValue(url, out var answer) ? answer() : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
