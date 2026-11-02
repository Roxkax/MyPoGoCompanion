using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.HttpResults;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.Diagnostics;

/// <summary>What broke in the web app.</summary>
public enum ClientErrorKind
{
    /// <summary>A screen broke while drawing ("Something went wrong").</summary>
    Screen,

    /// <summary>A script error or a failed promise nobody handled.</summary>
    Script,
}

/// <summary>An error in the web app, reported so it can be found in Application Insights.</summary>
/// <param name="Reference">The reference the app showed (8 to 32 hex characters, the start of the trace id).</param>
/// <param name="Kind">Whether a screen broke or a script failed.</param>
/// <param name="Message">The error's message.</param>
/// <param name="Stack">The stack trace, when the browser gives one.</param>
/// <param name="Path">The app's address where it happened, without the query.</param>
/// <param name="AppVersion">The web app's build (commit).</param>
public sealed record ClientErrorRequest(
    string? Reference, ClientErrorKind? Kind, string? Message, string? Stack, string? Path, string? AppVersion);

/// <summary>
/// <c>POST /client-errors</c> (decision 2026-11-02): the web app reports screen breaks and script errors here, and the
/// API logs them to Application Insights with personal data removed (the log filter does that for every log). No
/// sign-in needed; a signed-in report carries the user's random id. At most <see cref="ReportsPerMinute"/> a minute
/// per client address; long texts are cut.
/// </summary>
internal static partial class ClientErrorEndpoints
{
    public const string RateLimitPolicy = "client-errors";
    public const int ReportsPerMinute = 10;
    public const int MaxMessageLength = 1_000;
    public const int MaxStackLength = 8_000;
    public const int MaxPathLength = 200;
    public const int MaxVersionLength = 40;

    public static IServiceCollection AddClientErrors(this IServiceCollection services) =>
        services.AddRateLimiter(limiter => limiter.AddPolicy(RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            SignInSetup.ClientAddress(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = ReportsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

    public static IEndpointRouteBuilder MapClientErrorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/client-errors", Report)
            .RequireRateLimiting(RateLimitPolicy)
            .WithTags("Diagnostics")
            .WithName("ReportClientError")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithSummary("Reports an error in the web app (a screen that broke or a script error), so it can be found by its reference.");

        return app;
    }

    internal static Results<Accepted, ProblemHttpResult> Report(ClientErrorRequest request, ILoggerFactory loggers)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return TypedResults.Problem("The error's message is missing.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.Reference is not null && !ReferencePattern().IsMatch(request.Reference))
        {
            return TypedResults.Problem("The reference must be 8 to 32 hex characters.", statusCode: StatusCodes.Status400BadRequest);
        }

        var logger = loggers.CreateLogger("MyPoGoCompanion.WebApp");
        LogClientError(
            logger,
            request.Reference ?? "none",
            request.Kind ?? ClientErrorKind.Script,
            CleanPath(request.Path),
            Cut(request.AppVersion, MaxVersionLength) ?? "unknown",
            Cut(request.Message.Trim(), MaxMessageLength)!,
            Cut(request.Stack, MaxStackLength) ?? "(no stack trace)");
        return TypedResults.Accepted((string?)null);
    }

    /// <summary>Only the path: the query and fragment can hold codes or names (the sign-in callback has Google's code).</summary>
    internal static string CleanPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "unknown";
        }

        var end = path.IndexOfAny(['?', '#']);
        return Cut(end < 0 ? path.Trim() : path[..end].Trim(), MaxPathLength)!;
    }

    internal static string? Cut(string? text, int length) =>
        text is null ? null : text.Length <= length ? text : string.Concat(text.AsSpan(0, length), "…");

    [GeneratedRegex("^[0-9a-f]{8,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    [LoggerMessage(EventId = 9001, Level = LogLevel.Error,
        Message = "Web app error {Reference} ({Kind}) on {Path}, web version {AppVersion}: {ErrorMessage}\n{ErrorStack}")]
    private static partial void LogClientError(
        ILogger logger, string reference, ClientErrorKind kind, string path, string appVersion, string errorMessage, string errorStack);
}
