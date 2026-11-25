using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Users;
using MyPoGoCompanion.Engine.Search;

namespace MyPoGoCompanion.Api.Filters;

public sealed record SaveFilterRequest(string? Name, string? SearchString);
public sealed record NameFilterRequest(string? Name);
public sealed record SavedFilterResponse(Guid Id, string Name, string SearchString, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

internal static class SavedFilterEndpoints
{
    public static IEndpointRouteBuilder MapSavedFilterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/saved-filters")
            .RequireAuthorization()
            .WithTags("Filters")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers.CacheControl = "no-store";
                return await next(context);
            });

        group.MapGet("", ListAsync).WithName("ListSavedFilters")
            .WithSummary("Lists the signed-in account's saved searches.");
        group.MapPost("", SaveAsync).WithName("SaveFilter")
            .WithSummary("Saves a search to the signed-in account.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithMetadata(new RequestSizeLimitAttribute(128 * 1024));
        group.MapGet("/{id:guid}", GetAsync).WithName("GetSavedFilter")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Reads one of the signed-in account's saved searches.");
        group.MapPut("/{id:guid}/name", RenameAsync).WithName("RenameSavedFilter")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Renames one of the signed-in account's saved searches.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithMetadata(new RequestSizeLimitAttribute(4096));
        group.MapPost("/{id:guid}/duplicate", DuplicateAsync).WithName("DuplicateSavedFilter")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Creates a named copy of a saved search in the same account.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithMetadata(new RequestSizeLimitAttribute(4096));
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteSavedFilter")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Deletes one of the signed-in account's saved searches.");

        return app;
    }

    private static IQueryable<SavedFilter> Owned(AppDbContext db, ClaimsPrincipal user)
    {
        var userId = AccessTokens.UserId(user);
        return db.SavedFilters.Where(filter => filter.UserId == userId);
    }

    private static async Task<Ok<List<SavedFilterResponse>>> ListAsync(
        ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await Owned(db, user)
            .OrderByDescending(filter => filter.CreatedAt)
            .ThenByDescending(filter => filter.Id)
            .Select(filter => new SavedFilterResponse(
                filter.Id, filter.Name, filter.SearchString, filter.CreatedAt, filter.UpdatedAt))
            .ToListAsync(cancellationToken));

    private static async Task<Results<Ok<SavedFilterResponse>, NotFound>> GetAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        var filter = await Owned(db, user).AsNoTracking().SingleOrDefaultAsync(filter => filter.Id == id, cancellationToken);
        return filter is null ? TypedResults.NotFound() : TypedResults.Ok(ToResponse(filter));
    }

    private static async Task<Results<Created<SavedFilterResponse>, ProblemHttpResult, UnauthorizedHttpResult>> SaveAsync(
        SaveFilterRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var userId = AccessTokens.UserId(user);
        if (userId is null || !await db.Users.AnyAsync(entry => entry.Id == userId, cancellationToken))
        {
            return TypedResults.Unauthorized();
        }

        var name = request.Name?.Trim();
        if (!ValidName(name))
        {
            return InvalidName();
        }

        var search = request.SearchString;
        if (search is null || search.Contains('\0') || BuilderSearch.Check(search).Parts.Count == 0)
        {
            return TypedResults.Problem("Enter a search that can be copied into Pokémon GO.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await AddAsync(userId.Value, name!, search, null, db, time, cancellationToken);
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> RenameAsync(
        Guid id, NameFilterRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (!ValidName(name))
        {
            return InvalidName();
        }

        var now = time.GetUtcNow();
        var changed = await Owned(db, user).Where(filter => filter.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(filter => filter.Name, name!)
                .SetProperty(filter => filter.UpdatedAt, now), cancellationToken);
        return changed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<Results<Created<SavedFilterResponse>, NotFound, ProblemHttpResult>> DuplicateAsync(
        Guid id, NameFilterRequest request, ClaimsPrincipal user, AppDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (!ValidName(name))
        {
            return InvalidName();
        }

        var original = await Owned(db, user).AsNoTracking().SingleOrDefaultAsync(filter => filter.Id == id, cancellationToken);
        return original is null
            ? TypedResults.NotFound()
            : await AddAsync(original.UserId, name!, original.SearchString, original.Conditions, db, time, cancellationToken);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken cancellationToken)
    {
        var removed = await Owned(db, user).Where(filter => filter.Id == id).ExecuteDeleteAsync(cancellationToken);
        return removed == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    internal static bool ValidName(string? name) => name is { Length: > 0 and <= 100 } && !name.Any(char.IsControl);

    private static ProblemHttpResult InvalidName() =>
        TypedResults.Problem("Enter a name between 1 and 100 characters.", statusCode: StatusCodes.Status400BadRequest);

    private static async Task<Created<SavedFilterResponse>> AddAsync(
        Guid userId, string name, string search, string? conditions, AppDbContext db, TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var filter = new SavedFilter
        {
            UserId = userId,
            Name = name,
            SearchString = search,
            Conditions = conditions,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.SavedFilters.Add(filter);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/saved-filters/{filter.Id}", ToResponse(filter));
    }

    private static SavedFilterResponse ToResponse(SavedFilter filter) =>
        new(filter.Id, filter.Name, filter.SearchString, filter.CreatedAt, filter.UpdatedAt);
}
