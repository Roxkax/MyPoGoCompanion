using System.Diagnostics;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Observability;
using OpenTelemetry.Trace;

namespace MyPoGoCompanion.Api.Diagnostics;

/// <summary>
/// The API's telemetry (E01-F08, docs/OBSERVABILITY.md): the shared setup plus every request. A request carries the
/// signed-in user's random id (<c>enduser.id</c>), never an email or name. The web app sends a W3C
/// <c>traceparent</c> with each call, so the trace id it shows as an error's reference is the request's id in
/// Application Insights.
/// </summary>
internal static class ApiTelemetry
{
    /// <summary>The API's role name in Application Insights.</summary>
    public const string ServiceName = "api";

    /// <summary>The attribute Application Insights shows as the authenticated user id.</summary>
    public const string UserIdTag = "enduser.id";

    public static WebApplicationBuilder AddApiTelemetry(this WebApplicationBuilder builder)
    {
        builder.AddTelemetry(ServiceName, traces => traces.AddAspNetCoreInstrumentation(options => options.Filter = IsTraced));
        return builder;
    }

    /// <summary>Requests worth recording: not the liveness check (it says nothing) nor CORS preflights.</summary>
    internal static bool IsTraced(HttpContext context) =>
        !context.Request.Path.StartsWithSegments("/health/live", StringComparison.OrdinalIgnoreCase)
        && !HttpMethods.IsOptions(context.Request.Method);

    /// <summary>After authentication: tags the request with the user's random id when signed in.</summary>
    public static IApplicationBuilder UseUserIdInTelemetry(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            TagUser(Activity.Current, context);
            return next(context);
        });

    internal static void TagUser(Activity? activity, HttpContext context)
    {
        if (activity is not null && AccessTokens.UserId(context.User) is { } userId)
        {
            activity.SetTag(UserIdTag, userId.ToString());
        }
    }
}
