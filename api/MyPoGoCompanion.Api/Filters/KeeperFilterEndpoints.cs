using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Rankings;

namespace MyPoGoCompanion.Api.Filters;

/// <summary>The keeper filter parts published by the latest daily import.</summary>
public sealed record KeeperFiltersResponse(IReadOnlyList<KeeperFilterPart> Filters);

/// <summary>A copyable Pokémon GO storage search string in English.</summary>
/// <param name="Kind">Which keeper filter this part belongs to.</param>
/// <param name="Variant">Empty for the default, or the variant the player selected.</param>
/// <param name="Part">The one-based part number within the selected filter.</param>
/// <param name="FromDex">First Pokédex number in a PvP part.</param>
/// <param name="ToDex">Last Pokédex number in a PvP part.</param>
/// <param name="SearchString">The text to paste into Pokémon GO storage search, at most 1,950 characters.</param>
public sealed record KeeperFilterPart(KeeperFilterKind Kind, string Variant, int Part, int? FromDex, int? ToDex, string SearchString);

internal static class KeeperFilterEndpoints
{
    public static IEndpointRouteBuilder MapKeeperFilterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/keeper-filters", GetAsync)
            .WithName("GetKeeperFilters")
            .WithTags("Filters")
            .WithSummary("Ready-made keeper filters from the daily game data import.")
            .Produces<KeeperFiltersResponse>();
        return app;
    }

    private static async Task<KeeperFiltersResponse> GetAsync(AppDbContext db, CancellationToken cancellationToken) =>
        new(await db.KeeperFilters.AsNoTracking()
            .OrderBy(filter => filter.Kind).ThenBy(filter => filter.Variant).ThenBy(filter => filter.Part)
            .Select(filter => new KeeperFilterPart(filter.Kind, filter.Variant, filter.Part, filter.FromDex, filter.ToDex, filter.SearchString))
            .ToListAsync(cancellationToken));
}
