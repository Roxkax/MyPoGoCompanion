using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.Account;

/// <param name="Language"><c>en</c> or <c>pt-PT</c>.</param>
public sealed record LanguageRequest(string? Language);

/// <summary>The signed-in person's own settings.</summary>
internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/me/language", SetLanguageAsync)
            .RequireAuthorization()
            .WithTags("Account")
            .WithName("SetLanguage")
            .WithSummary("Saves the interface language (en or pt-PT) with the account, so every device signed in shows it.");

        return app;
    }

    internal static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> SetLanguageAsync(
        LanguageRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        if (!Languages.IsSupported(request.Language))
        {
            return TypedResults.Problem(
                $"The language must be one of: {string.Join(", ", Languages.All)}.", statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = AccessTokens.UserId(user);
        var stored = userId is null ? null : await db.Users.FindAsync([userId.Value], cancellationToken);
        if (stored is null)
        {
            return TypedResults.Unauthorized();
        }

        stored.Language = request.Language!;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
