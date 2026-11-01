using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Configuration;
using MyPoGoCompanion.Data;

namespace MyPoGoCompanion.Api.Auth;

public sealed record GoogleClientResponse(string ClientId);

public sealed record GoogleSignInRequest(string? Code, string? CodeVerifier, string? RedirectUri);

public sealed record RefreshRequest(string? RefreshToken);

internal static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/auth").WithTags("Sign-in").RequireRateLimiting(RateLimitPolicy);

        auth.MapGet("/google/client", GetGoogleClient)
            .WithName("GetGoogleClient")
            .WithSummary("The Google OAuth client id the web app sends people to Google with (503 when sign-in isn't set up).");

        auth.MapPost("/google", SignInWithGoogleAsync)
            .WithName("SignInWithGoogle")
            .WithSummary("Swaps Google's one-time code (authorization code + PKCE) for our own access and refresh tokens.");

        auth.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .WithSummary("Swaps a refresh token for a new access token and the next refresh token (401: sign in again).");

        auth.MapPost("/sign-out", SignOutAsync)
            .WithName("SignOut")
            .WithSummary("Ends this device's sign-in.");

        auth.MapPost("/sign-out-everywhere", SignOutEverywhereAsync)
            .RequireAuthorization()
            .WithName("SignOutEverywhere")
            .WithSummary("Ends every sign-in of the signed-in user, on every device.");

        app.MapGet("/me", GetMeAsync)
            .RequireAuthorization()
            .WithTags("Account")
            .WithName("GetMe")
            .WithSummary("The signed-in user: name, email, picture, role and settings.");

        return app;
    }

    internal static Results<Ok<GoogleClientResponse>, ProblemHttpResult> GetGoogleClient(IOptions<AuthOptions> options) =>
        options.Value.IsConfigured
            ? TypedResults.Ok(new GoogleClientResponse(options.Value.GoogleClientId!))
            : NotConfigured();

    internal static async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> SignInWithGoogleAsync(
        GoogleSignInRequest request, IOptions<AuthOptions> options, IConfiguration configuration,
        IGoogleTokenExchange google, Sessions sessions, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return NotConfigured();
        }

        if (!IsValidCode(request.Code) || !IsValidCodeVerifier(request.CodeVerifier))
        {
            return TypedResults.Problem("The sign-in request is incomplete.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!IsAllowedRedirectUri(request.RedirectUri, configuration))
        {
            return TypedResults.Problem("The return address isn't one of the app's addresses.", statusCode: StatusCodes.Status400BadRequest);
        }

        var identity = await google.ExchangeAsync(request.Code!, request.CodeVerifier!, request.RedirectUri!, cancellationToken);
        return identity is null
            ? TypedResults.Problem("Google didn't confirm the sign-in. Please try again.", statusCode: StatusCodes.Status400BadRequest)
            : TypedResults.Ok(await sessions.SignInAsync(identity, cancellationToken));
    }

    internal static async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>> RefreshAsync(
        RefreshRequest request, Sessions sessions, CancellationToken cancellationToken) =>
        await sessions.RefreshAsync(request.RefreshToken ?? string.Empty, cancellationToken) is { } response
            ? TypedResults.Ok(response)
            : TypedResults.Unauthorized();

    internal static async Task<NoContent> SignOutAsync(RefreshRequest request, Sessions sessions, CancellationToken cancellationToken)
    {
        await sessions.SignOutAsync(request.RefreshToken ?? string.Empty, cancellationToken);
        return TypedResults.NoContent();
    }

    internal static async Task<Results<NoContent, UnauthorizedHttpResult>> SignOutEverywhereAsync(
        ClaimsPrincipal user, Sessions sessions, CancellationToken cancellationToken)
    {
        if (AccessTokens.UserId(user) is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        await sessions.SignOutEverywhereAsync(userId, cancellationToken);
        return TypedResults.NoContent();
    }

    internal static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetMeAsync(
        ClaimsPrincipal user, AppDbContext db, IOptions<AuthOptions> options, CancellationToken cancellationToken)
    {
        var userId = AccessTokens.UserId(user);
        var stored = userId is null ? null : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return stored is null ? TypedResults.Unauthorized() : TypedResults.Ok(Sessions.Me(stored, options.Value));
    }

    /// <summary>Google's codes are opaque; a sane length keeps junk away from Google.</summary>
    internal static bool IsValidCode(string? code) => code is { Length: > 0 and <= 512 } && !code.Any(char.IsWhiteSpace);

    /// <summary>RFC 7636: 43–128 characters from A–Z, a–z, 0–9 and <c>-._~</c>.</summary>
    internal static bool IsValidCodeVerifier(string? verifier) =>
        verifier is { Length: >= 43 and <= 128 } && verifier.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~');

    /// <summary>Only an allowed web origin (<c>Cors:AllowedOrigins</c>) followed by <c>/auth/callback</c>.</summary>
    internal static bool IsAllowedRedirectUri(string? redirectUri, IConfiguration configuration) =>
        redirectUri is not null
        && CorsSetup.ParseOrigins(configuration["Cors:AllowedOrigins"])
            .Any(origin => string.Equals(origin + AuthOptions.CallbackPath, redirectUri, StringComparison.Ordinal));

    private static ProblemHttpResult NotConfigured() =>
        TypedResults.Problem("Sign-in isn't set up on this server yet.", statusCode: StatusCodes.Status503ServiceUnavailable);
}
