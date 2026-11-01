using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Auth;

namespace MyPoGoCompanion.Api.UnitTests.Auth;

public class SignInSetupTests
{
    private static ServiceProvider Build(params (string Key, string? Value)[] values) =>
        new ServiceCollection()
            .AddLogging()
            .AddSignIn(new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
                .Build())
            .BuildServiceProvider();

    [Fact]
    public void AddSignIn_ReadsTheAuthSection()
    {
        using var services = Build(
            ("Auth:Google:ClientId", "id"), ("Auth:Google:ClientSecret", "secret"),
            ("Auth:SigningKey", AuthTestKit.SigningKeyText), ("Auth:SuperAdminEmail", "owner@example.com"));

        var options = services.GetRequiredService<IOptions<AuthOptions>>().Value;

        Assert.True(options.IsConfigured);
        Assert.True(options.IsSuperAdmin("owner@example.com"));
        Assert.Equal(options.SigningKeyBytes(), services.GetRequiredService<SigningKey>().Value.Key);
    }

    [Fact]
    public void AddSignIn_NothingConfigured_StillStarts()
    {
        using var services = Build();

        Assert.False(services.GetRequiredService<IOptions<AuthOptions>>().Value.IsConfigured);
        Assert.NotNull(services.GetRequiredService<AccessTokens>());
        Assert.NotNull(services.GetRequiredService<IGoogleTokenExchange>());
    }

    [Fact]
    public async Task AddSignIn_RegistersBothRolePolicies()
    {
        using var services = Build();
        var policies = services.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await policies.GetPolicyAsync(Policies.Admin));
        Assert.NotNull(await policies.GetPolicyAsync(Policies.SuperAdmin));
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData(null, "255.255.255.255")]
    public void ClientAddress_UsesTheConnectionAddress(string? address, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address is null ? null : IPAddress.Parse(address);

        Assert.Equal(expected, SignInSetup.ClientAddress(context));
    }
}
