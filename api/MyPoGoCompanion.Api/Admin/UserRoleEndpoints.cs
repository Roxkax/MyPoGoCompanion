using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.Admin;

public sealed record AdminUserResponse(Guid Id, string Name, string Email, string? PictureUrl, EffectiveRole Role, DateTimeOffset LastSignInAt);

public sealed record SetRoleRequest(UserRole? Role);

/// <summary>
/// Users & roles, super admin only: list users and make someone admin or remove it. The screen comes with the
/// admin panel (E10-F01, issue #924); these endpoints are what it will call.
/// </summary>
internal static class UserRoleEndpoints
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static IEndpointRouteBuilder MapUserRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/admin/users").WithTags("Admin").RequireAuthorization(Policies.SuperAdmin);

        users.MapGet("/", ListUsersAsync)
            .WithName("ListUsers")
            .WithSummary("Users by name, optionally filtered by name or email (super admin only).");

        users.MapPut("/{id:guid}/role", SetRoleAsync)
            .WithName("SetUserRole")
            .WithSummary("Makes a user admin or removes it (super admin only). Every change is kept.");

        return app;
    }

    internal static async Task<Ok<AdminUserResponse[]>> ListUsersAsync(
        AppDbContext db, IOptions<AuthOptions> options, string? search, int? limit, CancellationToken cancellationToken)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = "%" + search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(u => EF.Functions.ILike(u.DisplayName, pattern) || EF.Functions.ILike(u.Email, pattern));
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var found = await query.OrderBy(u => u.DisplayName).ThenBy(u => u.Email).Take(take).ToListAsync(cancellationToken);
        return TypedResults.Ok(found.Select(u => ToResponse(u, options.Value)).ToArray());
    }

    internal static async Task<Results<Ok<AdminUserResponse>, NotFound, ProblemHttpResult>> SetRoleAsync(
        Guid id, SetRoleRequest request, ClaimsPrincipal caller, AppDbContext db, IOptions<AuthOptions> options,
        TimeProvider time, CancellationToken cancellationToken)
    {
        if (request.Role is not { } role || !Enum.IsDefined(role))
        {
            return TypedResults.Problem("Choose User or Admin.", statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (options.Value.IsSuperAdmin(user.Email))
        {
            return TypedResults.Problem("The super admin's role comes from configuration and can't be changed here.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (user.Role != role)
        {
            var now = time.GetUtcNow();
            var callerId = AccessTokens.UserId(caller);
            db.RoleChanges.Add(new RoleChange { At = now, UserId = user.Id, ChangedByUserId = callerId, From = user.Role, To = role });
            user.Role = role;
            user.RoleGrantedAt = now;
            user.RoleGrantedByUserId = callerId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToResponse(user, options.Value));
    }

    private static AdminUserResponse ToResponse(User user, AuthOptions options) =>
        new(user.Id, user.DisplayName, user.Email, user.PictureUrl, Sessions.RoleOf(user, options), user.LastSignInAt);
}
