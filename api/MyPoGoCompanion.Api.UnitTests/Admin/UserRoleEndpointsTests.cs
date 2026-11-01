using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Api.UnitTests.Auth;
using MyPoGoCompanion.Data.Users;

namespace MyPoGoCompanion.Api.UnitTests.Admin;

public class UserRoleEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ClaimsPrincipal Signed(Guid id) => new(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));

    [Fact]
    public async Task ListUsers_SortedByNameWithEffectiveRoles()
    {
        using var kit = new AuthTestKit();
        await kit.AddUserAsync("misty@example.com", UserRole.Admin, "Misty");
        await kit.AddUserAsync(AuthTestKit.SuperAdminEmail, name: "Diogo");
        await kit.AddUserAsync("brock@example.com", name: "Brock");

        var ok = await UserRoleEndpoints.ListUsersAsync(kit.Db, kit.OptionsAccessor, null, null, Ct);

        Assert.Equal(
            [("Brock", EffectiveRole.User), ("Diogo", EffectiveRole.SuperAdmin), ("Misty", EffectiveRole.Admin)],
            ok.Value!.Select(u => (u.Name, u.Role)));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    [InlineData(500, 3)]
    public async Task ListUsers_LimitIsClamped(int limit, int expected)
    {
        using var kit = new AuthTestKit();
        foreach (var name in new[] { "A", "B", "C" })
        {
            await kit.AddUserAsync($"{name}@example.com", name: name);
        }

        var ok = await UserRoleEndpoints.ListUsersAsync(kit.Db, kit.OptionsAccessor, null, limit, Ct);

        Assert.Equal(expected, ok.Value!.Length);
    }

    [Fact]
    public async Task SetRole_MakeAdmin_ChangesTheRoleAndKeepsTheChange()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);
        var misty = await kit.AddUserAsync("misty@example.com", name: "Misty");

        var result = await UserRoleEndpoints.SetRoleAsync(misty.Id, new SetRoleRequest(UserRole.Admin), Signed(owner.Id), kit.Db, kit.OptionsAccessor, kit.Clock, Ct);

        Assert.Equal(EffectiveRole.Admin, Assert.IsType<Ok<AdminUserResponse>>(result.Result).Value!.Role);
        var stored = await kit.NewContext().Users.SingleAsync(u => u.Id == misty.Id, Ct);
        Assert.Equal((UserRole.Admin, kit.Clock.Now, owner.Id), (stored.Role, stored.RoleGrantedAt!.Value, stored.RoleGrantedByUserId!.Value));
        var change = await kit.NewContext().RoleChanges.SingleAsync(Ct);
        Assert.Equal((misty.Id, owner.Id, UserRole.User, UserRole.Admin, kit.Clock.Now), (change.UserId, change.ChangedByUserId!.Value, change.From, change.To, change.At));
    }

    [Fact]
    public async Task SetRole_SameRole_ChangesNothing()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);
        var brock = await kit.AddUserAsync("brock@example.com");

        var result = await UserRoleEndpoints.SetRoleAsync(brock.Id, new SetRoleRequest(UserRole.User), Signed(owner.Id), kit.Db, kit.OptionsAccessor, kit.Clock, Ct);

        Assert.IsType<Ok<AdminUserResponse>>(result.Result);
        Assert.Empty(kit.Db.RoleChanges);
    }

    [Fact]
    public async Task SetRole_UnknownUser_Returns404()
    {
        using var kit = new AuthTestKit();

        var result = await UserRoleEndpoints.SetRoleAsync(Guid.NewGuid(), new SetRoleRequest(UserRole.Admin), Signed(Guid.NewGuid()), kit.Db, kit.OptionsAccessor, kit.Clock, Ct);

        Assert.IsType<NotFound>(result.Result);
    }

    [Fact]
    public async Task SetRole_TheSuperAdmin_Returns400()
    {
        using var kit = new AuthTestKit();
        var owner = await kit.AddUserAsync(AuthTestKit.SuperAdminEmail);

        var result = await UserRoleEndpoints.SetRoleAsync(owner.Id, new SetRoleRequest(UserRole.Admin), Signed(owner.Id), kit.Db, kit.OptionsAccessor, kit.Clock, Ct);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((UserRole)7)]
    public async Task SetRole_NoOrUnknownRole_Returns400(UserRole? role)
    {
        using var kit = new AuthTestKit();
        var brock = await kit.AddUserAsync("brock@example.com");

        var result = await UserRoleEndpoints.SetRoleAsync(brock.Id, new SetRoleRequest(role), Signed(Guid.NewGuid()), kit.Db, kit.OptionsAccessor, kit.Clock, Ct);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }
}
