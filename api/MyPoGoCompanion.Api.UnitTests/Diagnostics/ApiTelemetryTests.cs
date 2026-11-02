using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MyPoGoCompanion.Api.Diagnostics;
using OpenTelemetry.Trace;

namespace MyPoGoCompanion.Api.UnitTests.Diagnostics;

public class ApiTelemetryTests
{
    [Theory]
    [InlineData("GET", "/health/live", false)]
    [InlineData("GET", "/HEALTH/LIVE", false)]
    [InlineData("OPTIONS", "/me", false)]
    [InlineData("GET", "/health", true)]
    [InlineData("POST", "/client-errors", true)]
    public void Records_requests_except_liveness_and_preflights(string method, string path, bool traced)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        Assert.Equal(traced, ApiTelemetry.IsTraced(context));
    }

    [Fact]
    public void A_signed_in_request_carries_the_user_id_only()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()), new Claim(JwtRegisteredClaimNames.Email, "ash.pt@example.com")], "Bearer")),
        };
        using var activity = new Activity("GET /me");

        ApiTelemetry.TagUser(activity, context);

        Assert.Equal(userId.ToString(), activity.GetTagItem(ApiTelemetry.UserIdTag));
        Assert.Single(activity.TagObjects);
    }

    [Fact]
    public void A_signed_out_request_or_no_request_span_carries_nothing()
    {
        using var activity = new Activity("GET /health");
        ApiTelemetry.TagUser(activity, new DefaultHttpContext());
        ApiTelemetry.TagUser(null, new DefaultHttpContext());
        Assert.Empty(activity.TagObjects);
    }

    [Fact]
    public async Task The_middleware_tags_the_current_request()
    {
        var userId = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())], "Bearer")),
        };
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        app.UseUserIdInTelemetry();
        var reached = false;
        app.Run(_ =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        using var activity = new Activity("GET /me").Start();
        await app.Build()(context);

        Assert.True(reached);
        Assert.Equal(userId.ToString(), activity.GetTagItem(ApiTelemetry.UserIdTag));
    }

    [Fact]
    public void With_a_connection_string_the_api_exports_its_requests()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.invalid/";
        builder.AddApiTelemetry();
        using var app = builder.Build();
        Assert.NotNull(app.Services.GetService<TracerProvider>());
    }
}
