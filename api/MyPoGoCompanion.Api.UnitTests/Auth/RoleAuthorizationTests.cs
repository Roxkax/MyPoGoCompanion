using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class RoleAuthorizationTests
{
    private static async Task<bool> AllowedAsync(AuthTestKit kit, ClaimsPrincipal user, EffectiveRole minimum)
    {
        var requirement = new RoleRequirement(minimum);
        var context = new AuthorizationHandlerContext([requirement], user, null);
        await new RoleRequirementHandler(kit.Db, kit.OptionsAccessor).HandleAsync(context);
        return context.HasSucceeded;
    }

    private static ClaimsPrincipal Signed(Guid id) => new(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));

    [Theory]
    [InlineData("brock@example.com", UserRole.User, EffectiveRole.Admin, false)]
    [InlineData("misty@example.com", UserRole.Admin, EffectiveRole.Admin, true)]
    [InlineData("misty@example.com", UserRole.Admin, EffectiveRole.SuperAdmin, false)]
    [InlineData(AuthTestKit.SuperAdminEmail, UserRole.User, EffectiveRole.Admin, true)]
    [InlineData(AuthTestKit.SuperAdminEmail, UserRole.User, EffectiveRole.SuperAdmin, true)]
    public async Task Handler_ChecksTheRoleInTheDatabase(string email, UserRole stored, EffectiveRole minimum, bool expected)
    {
        using var kit = new AuthTestKit();
        var user = await kit.AddUserAsync(email, stored);

        Assert.Equal(expected, await AllowedAsync(kit, Signed(user.Id), minimum));
    }

    [Fact]
    public async Task Handler_RoleRemovedAfterSignIn_IsRefusedAtOnce()
    {
        using var kit = new AuthTestKit();
        var user = await kit.AddUserAsync("misty@example.com", UserRole.Admin);
        user.Role = UserRole.User;
        await kit.Db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(await AllowedAsync(kit, Signed(user.Id), EffectiveRole.Admin));
    }

    [Fact]
    public async Task Handler_UnknownOrMissingUser_IsRefused()
    {
        using var kit = new AuthTestKit();

        Assert.False(await AllowedAsync(kit, Signed(Guid.NewGuid()), EffectiveRole.Admin));
        Assert.False(await AllowedAsync(kit, new ClaimsPrincipal(), EffectiveRole.Admin));
    }
}
