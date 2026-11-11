using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyPoGoCompanion.Api.Account;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Api.Diagnostics;
using MyPoGoCompanion.Api.Freshness;
using MyPoGoCompanion.Api.Health;
using MyPoGoCompanion.Api.Moves;
using MyPoGoCompanion.Api.Pokedex;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Data.LiveData;
using MyPoGoCompanion.Data.Rankings;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.OpenApi;

/// <summary>
/// Examples shown in the OpenAPI document and the docs page, written as the real C# types so they can't drift from
/// the shape the API sends. They use the design's sample trainer (Ash_PT) and made-up tokens. Every request and
/// response type needs one (an integration test checks the document).
/// </summary>
internal static class OpenApiExamples
{
    private static readonly Guid _ashId = Guid.Parse("0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10");
    private static readonly DateTimeOffset _now = new(2026, 10, 27, 18, 30, 0, TimeSpan.Zero);

    private static readonly MeResponse _ash = new(
        _ashId, "ash.pt@example.com", "Ash_PT", "https://lh3.googleusercontent.com/a/ash-pt", EffectiveRole.User, Languages.Portuguese, Theme.Device);

    // Game data imported this morning; raids and events last updated 12 days ago (over the 9-day limit).
    private static readonly DataFreshness _gameDataUpToDate = new(new DateTimeOffset(2026, 10, 27, 6, 4, 0, TimeSpan.Zero), 0, OutOfDate: false, 3);
    private static readonly DataFreshness _liveDataOutOfDate = new(new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero), 12, OutOfDate: true, 9);

    // Charizard with one of its versions, and two of its moves.
    private static readonly PokedexForm _megaCharizardY = new(
        "charizard_mega_y", FormKind.Mega, IsDefault: false, "Mega Charizard Y", "Mega Charizard Y", "Mega Y", "Mega Y", ["fire", "flying"],
        319, 212, 186, 5037, Released: true, Shadow: false, Dynamax: false, Gigantamax: false, "pm6.fMEGA_Y", null, LooksOnly: false,
        ["AIR_SLASH", "DRAGON_BREATH", "EMBER", "FIRE_SPIN", "WING_ATTACK"], ["BLAST_BURN", "DRAGON_CLAW", "FIRE_BLAST", "FLAMETHROWER", "OVERHEAT"]);

    private static readonly PokedexSpecies _charizard = new(
        6, "Charizard", "Charizard", 1, SpeciesCategory.None, "FAMILY_CHARMANDER", ["zard"],
        [
            new PokedexForm(
                "charizard", FormKind.Base, IsDefault: true, "Charizard", "Charizard", null, null, ["fire", "flying"],
                223, 173, 186, 3266, Released: true, Shadow: true, Dynamax: true, Gigantamax: true, "pm6", "pm6.fGIGANTAMAX", LooksOnly: false,
                ["AIR_SLASH", "DRAGON_BREATH", "EMBER", "FIRE_SPIN", "WING_ATTACK"], ["BLAST_BURN", "DRAGON_CLAW", "FIRE_BLAST", "FLAMETHROWER", "OVERHEAT"]),
            _megaCharizardY,
        ]);

    private static readonly PokedexMove _blastBurn = new("BLAST_BURN", "Blast Burn", "Queimadura Explosiva", "fire", MoveKind.Charged);

    // Garchomp's page (B2), trimmed to a few of each list.
    private static readonly LevelCpRow _garchompL40 = new(40m, 3962);
    private static readonly PageFormMove _dragonTail = new("DRAGON_TAIL", MoveAvailability.Normal);
    private static readonly TypeMultiplier _ice = new("ice", 2.56);
    private static readonly MoveUses _dragonTailUses = new("DRAGON_TAIL", 71250);
    private static readonly PagePvpRanking _garchompMaster = new(
        League.Master, IsShadow: false, 31, 88.4, "DRAGON_TAIL", "EARTH_POWER", "OUTRAGE", [_dragonTailUses, new MoveUses("EARTH_POWER", 48211)]);
    private static readonly PageBestIvs _garchompGreat = new(League.Great, 0, 15, 14, 14m, 1495);
    private static readonly PageKeeperBox _gibleGreatBox = new(League.Great, 0, 1, 3, 4, 2, 4);
    private static readonly PageRaidRank _groundRank = new("ground", IsShadow: false, 12, "MUD_SHOT", "EARTH_POWER");
    private static readonly PageRaidMoveset _mudShotEarthPower = new(IsShadow: false, "MUD_SHOT", "EARTH_POWER", 1, 17.82, 612.5, 41.3);
    private static readonly PageForm _garchomp = new(
        "garchomp", FormKind.Base, IsDefault: true, "Garchomp", "Garchomp", null, null, ["dragon", "ground"], 261, 193, 239,
        [_garchompL40, new LevelCpRow(50m, 4479), new LevelCpRow(51m, 4532)],
        [new LevelCpRow(15m, 1698), new LevelCpRow(20m, 2264), new LevelCpRow(25m, 2830), new LevelCpRow(30m, 3396), new LevelCpRow(35m, 3679)],
        Released: true, Shadow: true, Shiny: true, Dynamax: false, Gigantamax: false, "pm445", "pm445.s", null, LooksOnly: false, 5, 75000,
        [_dragonTail, new PageFormMove("EARTH_POWER", MoveAvailability.EliteTm)],
        [_ice, new TypeMultiplier("electric", 0.244)],
        [_garchompMaster], [_garchompGreat], [_groundRank], [_mudShotEarthPower]);
    private static readonly PageMove _dragonTailMove = new(
        "DRAGON_TAIL", "Dragon Tail", "Cauda do Dragão", "dragon", MoveKind.Fast, 14, 1000, 8, 9, 12, 3, null, 0, 0, 0, 0);
    private static readonly FamilyMember _gible = new(443, "gible", FormKind.Base, IsDefault: true, "Gible", "Gible", "Gible", ["dragon", "ground"], 124, 84, 151, Released: true, "pm443");
    private static readonly PageEvolution _gibleToGabite = new("gible", "gabite", 25, null, null, null, null, null, null, null, null);
    private static readonly PageCostume _costume = new("pm445.cHOLIDAY_2025", "Holiday 2025", null);
    private static readonly PageAppearance _communityDay = new(
        AppearanceKind.Event, "garchomp", IsShadow: false, null, null, Gigantamax: false, null, null, null, "Community Day: Gible",
        "Dia Comunitário: Gible", EventKind.CommunityDay, new DateTime(2026, 11, 7, 14, 0, 0), new DateTime(2026, 11, 7, 17, 0, 0), Shiny: true);

    private static readonly Dictionary<Type, object> _examples = new()
    {
        [typeof(HealthResponse)] = new HealthResponse(
            HealthStatus.Healthy, "3cd4b0c", new Dictionary<string, HealthStatus>(StringComparer.Ordinal) { ["database"] = HealthStatus.Healthy }),
        [typeof(GoogleClientResponse)] = new GoogleClientResponse("123456789012-abc123def456.apps.googleusercontent.com"),
        [typeof(GoogleSignInRequest)] = new GoogleSignInRequest(
            "4/0AeanS0a1b2c3d4e5f6g7h8i9j0", "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk", "https://app.example/auth/callback", Languages.Portuguese),
        [typeof(RefreshRequest)] = new RefreshRequest("pX7k2Qm9vB4nR8sT1wY6zA3cF5hJ0lN2dG4eK7oU9iE"),
        [typeof(SessionResponse)] = new SessionResponse(
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwZjhjMmQxZSJ9.c2lnbmF0dXJl", _now.AddMinutes(15),
            "Hq3vN8xW2bL5mK9tR4yC7fJ1dS6gZ0aE8uP3iO5nT2w", _ash),
        [typeof(MeResponse)] = _ash,
        [typeof(LanguageRequest)] = new LanguageRequest(Languages.Portuguese),
        [typeof(AdminUserResponse)] = new AdminUserResponse(
            _ashId, "Ash_PT", "ash.pt@example.com", "https://lh3.googleusercontent.com/a/ash-pt", EffectiveRole.Admin, _now.AddHours(-2)),
        [typeof(SetRoleRequest)] = new SetRoleRequest(UserRole.Admin),
        [typeof(RunImportRequest)] = new RunImportRequest(PublishMissing: false),
        [typeof(RunImportResponse)] = new RunImportResponse("job-import-prod-k3x9q2m", PublishMissing: false, _now),
        [typeof(FreshnessResponse)] = new FreshnessResponse(_gameDataUpToDate, _liveDataOutOfDate),
        [typeof(DataFreshness)] = _liveDataOutOfDate,
        [typeof(PokedexResponse)] = new PokedexResponse([_charizard], [_blastBurn, new PokedexMove("FIRE_SPIN", "Fire Spin", "Chama Furacão", "fire", MoveKind.Fast)]),
        [typeof(PokedexSpecies)] = _charizard,
        [typeof(PokedexForm)] = _megaCharizardY,
        [typeof(PokedexMove)] = _blastBurn,
        [typeof(PokemonPageResponse)] = new PokemonPageResponse(
            445, "Garchomp", "Garchomp", 4, SpeciesCategory.None, "FAMILY_GIBLE", [_garchomp], [_gibleGreatBox], [_dragonTailMove],
            [_gible], [_gibleToGabite], [_costume], [_communityDay], [0.094, 0.1351374318, 0.16639787]),
        [typeof(PageForm)] = _garchomp,
        [typeof(LevelCpRow)] = _garchompL40,
        [typeof(PageFormMove)] = _dragonTail,
        [typeof(TypeMultiplier)] = _ice,
        [typeof(PagePvpRanking)] = _garchompMaster,
        [typeof(MoveUses)] = _dragonTailUses,
        [typeof(PageBestIvs)] = _garchompGreat,
        [typeof(PageKeeperBox)] = _gibleGreatBox,
        [typeof(PageRaidRank)] = _groundRank,
        [typeof(PageRaidMoveset)] = _mudShotEarthPower,
        [typeof(PageMove)] = _dragonTailMove,
        [typeof(MovesResponse)] = new MovesResponse([_dragonTailMove]),
        [typeof(FamilyMember)] = _gible,
        [typeof(PageEvolution)] = _gibleToGabite,
        [typeof(PageCostume)] = _costume,
        [typeof(PageAppearance)] = _communityDay,
        [typeof(ClientErrorRequest)] = new ClientErrorRequest(
            "7f3a2c91", ClientErrorKind.Screen, "Cannot read properties of undefined (reading 'cp')",
            "TypeError: Cannot read properties of undefined (reading 'cp')\n    at RaidCounters (assets/index-3cd4b0c.js:1:23456)",
            "/battle/raids/mewtwo", "3cd4b0c"),
        [typeof(ProblemDetails)] = Problem("rfc9110#section-15.5.1", "Bad Request", 400, "The language must be one of: en, pt-PT."),
    };

    /// <summary>What the API answers for each error status (title and type as ASP.NET Core writes them).</summary>
    private static readonly Dictionary<int, ProblemDetails> _problems = new()
    {
        [StatusCodes.Status400BadRequest] = Problem("rfc9110#section-15.5.1", "Bad Request", 400, "What was wrong with the request, written for people."),
        [StatusCodes.Status401Unauthorized] = Problem("rfc9110#section-15.5.2", "Unauthorized", 401, null),
        [StatusCodes.Status403Forbidden] = Problem("rfc9110#section-15.5.4", "Forbidden", 403, null),
        [StatusCodes.Status404NotFound] = Problem("rfc9110#section-15.5.5", "Not Found", 404, null),
        [StatusCodes.Status409Conflict] = Problem("rfc9110#section-15.5.10", "Conflict", 409, "A daily import is already running (started 2 minutes ago)."),
        [StatusCodes.Status429TooManyRequests] = Problem("rfc6585#section-4", "Too Many Requests", 429, null),
        [StatusCodes.Status502BadGateway] = Problem("rfc9110#section-15.6.3", "Bad Gateway", 502, "Couldn't start the job: Azure answered 500 Internal Server Error."),
        [StatusCodes.Status503ServiceUnavailable] = Problem("rfc9110#section-15.6.4", "Service Unavailable", 503, "Sign-in isn't set up on this server yet."),
    };

    private static ProblemDetails Problem(string section, string title, int status, string? detail) =>
        new() { Type = "https://tools.ietf.org/html/" + section, Title = title, Status = status, Detail = detail };

    /// <summary>The example error for this status, or null for a status without one.</summary>
    public static ProblemDetails? ForProblem(int status) => _problems.GetValueOrDefault(status);

    /// <summary>The example for this type, or null when it has none (simple values, enums, collections).</summary>
    public static object? For(Type type) => _examples.GetValueOrDefault(type);

    /// <summary>Every type that has an example.</summary>
    public static IEnumerable<Type> Types => _examples.Keys;
}
