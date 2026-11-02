using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyPoGoCompanion.Api.Account;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Api.Diagnostics;
using MyPoGoCompanion.Api.Freshness;
using MyPoGoCompanion.Api.Health;
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
        [typeof(FreshnessResponse)] = new FreshnessResponse(_gameDataUpToDate, _liveDataOutOfDate),
        [typeof(DataFreshness)] = _liveDataOutOfDate,
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
        [StatusCodes.Status429TooManyRequests] = Problem("rfc6585#section-4", "Too Many Requests", 429, null),
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
