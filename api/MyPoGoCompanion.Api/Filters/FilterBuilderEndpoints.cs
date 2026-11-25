using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Engine.Search;

namespace MyPoGoCompanion.Api.Filters;

/// <summary>A hand-edited or generated Pokémon GO storage search in English.</summary>
public sealed record FilterBuilderRequest(string? Expression);

internal static class FilterBuilderEndpoints
{
    public static IEndpointRouteBuilder MapFilterBuilderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/filter-search/check", CheckAsync)
            .WithName("CheckFilterSearch")
            .WithTags("Filters")
            .WithSummary("Checks a Pokémon GO storage search and splits it into pasteable parts when possible.")
            .WithMetadata(new RequestSizeLimitAttribute(128 * 1024))
            .Produces<BuilderSearch.SearchCheckResult>();
        return app;
    }

    private static async Task<BuilderSearch.SearchCheckResult> CheckAsync(
        FilterBuilderRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var initial = BuilderSearch.Check(request.Expression);
        if (initial.Status != BuilderSearch.SearchCheckStatus.Unverified)
        {
            return initial;
        }

        var species = await db.Species.AsNoTracking().Select(entry => entry.NameEn).ToListAsync(cancellationToken);
        var moves = await db.Moves.AsNoTracking().Select(move => move.NameEn).ToListAsync(cancellationToken);
        return BuilderSearch.Check(request.Expression, term =>
        {
            if (term.StartsWith('@'))
            {
                var start = term.Length > 1 && term[1] is '1' or '2' or '3' ? 2 : 1;
                return moves.Contains(term[start..], StringComparer.OrdinalIgnoreCase);
            }

            var name = term.StartsWith('+') ? term[1..] : term;
            return name.Length > 0 && species.Any(entry => entry.StartsWith(name, StringComparison.OrdinalIgnoreCase));
        });
    }
}
