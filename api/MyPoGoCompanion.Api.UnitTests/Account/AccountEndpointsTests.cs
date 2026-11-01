using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyPoGoCompanion.Api.Account;
using MyPoGoCompanion.Api.UnitTests.Auth;

namespace MyPoGoCompanion.Api.UnitTests.Account;

public class AccountEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ClaimsPrincipal Signed(Guid id) => new(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"));

    [Theory]
    [InlineData("pt-PT")]
    [InlineData("en")]
    public async Task SetLanguage_Supported_SavesItWithTheAccount(string language)
    {
        using var kit = new AuthTestKit();
        var user = await kit.AddUserAsync("misty@example.com");

        var result = await AccountEndpoints.SetLanguageAsync(new LanguageRequest(language), Signed(user.Id), kit.Db, Ct);

        Assert.IsType<NoContent>(result.Result);
        await using var fresh = kit.NewContext();
        Assert.Equal(language, (await fresh.Users.SingleAsync(Ct)).Language);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pt")]
    [InlineData("pt-BR")]
    [InlineData("EN")]
    public async Task SetLanguage_Unsupported_Returns400AndChangesNothing(string? language)
    {
        using var kit = new AuthTestKit();
        var user = await kit.AddUserAsync("misty@example.com");

        var result = await AccountEndpoints.SetLanguageAsync(new LanguageRequest(language), Signed(user.Id), kit.Db, Ct);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
        await using var fresh = kit.NewContext();
        Assert.Equal("en", (await fresh.Users.SingleAsync(Ct)).Language);
    }

    [Fact]
    public async Task SetLanguage_UnknownOrMissingUser_Returns401()
    {
        using var kit = new AuthTestKit();

        Assert.IsType<UnauthorizedHttpResult>(
            (await AccountEndpoints.SetLanguageAsync(new LanguageRequest("pt-PT"), Signed(Guid.NewGuid()), kit.Db, Ct)).Result);
        Assert.IsType<UnauthorizedHttpResult>(
            (await AccountEndpoints.SetLanguageAsync(new LanguageRequest("pt-PT"), new ClaimsPrincipal(), kit.Db, Ct)).Result);
    }
}
