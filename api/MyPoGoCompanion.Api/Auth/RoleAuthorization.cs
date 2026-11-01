using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data;

namespace MyPoGoCompanion.Api.Auth;

/// <summary>Policy names for admin endpoints.</summary>
public static class Policies
{
    /// <summary>Admins and the super admin: every admin task except changing roles.</summary>
    public const string Admin = "Admin";

    /// <summary>The super admin only: making people admin or removing it.</summary>
    public const string SuperAdmin = "SuperAdmin";
}

/// <summary>Needs at least this role, read from the database on every request (not from the token).</summary>
public sealed class RoleRequirement(EffectiveRole minimum) : IAuthorizationRequirement
{
    public EffectiveRole Minimum { get; } = minimum;
}

/// <summary>
/// Checks the role in the database rather than the access token, so removing someone's admin role applies to their
/// next admin request instead of when their token expires.
/// </summary>
public sealed class RoleRequirementHandler(AppDbContext db, IOptions<AuthOptions> options) : AuthorizationHandler<RoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);
        if (AccessTokens.UserId(context.User) is not { } userId)
        {
            return;
        }

        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        if (user is not null && Sessions.RoleOf(user, options.Value) >= requirement.Minimum)
        {
            context.Succeed(requirement);
        }
    }
}
