using Microsoft.AspNetCore.Http.HttpResults;
using MyPoGoCompanion.Ai;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.Admin;

internal static class AiBudgetEndpoints
{
    public static IEndpointRouteBuilder MapAiBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/ai/budget", GetAsync)
            .WithName("GetAiBudget")
            .WithTags("Admin")
            .RequireAuthorization(Policies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Shows shared AI spend, reservations, and the next UTC monthly reset (admins).");
        return app;
    }

    private static async Task<Ok<AiBudgetStatus>> GetAsync(
        HttpContext context, IAiBudget budget, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return TypedResults.Ok(await budget.GetStatusAsync(cancellationToken));
    }
}
