using System.Text.Json.Serialization;
using MyPoGoCompanion.Api.Account;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Api.Configuration;
using MyPoGoCompanion.Api.Freshness;
using MyPoGoCompanion.Api.Health;
using MyPoGoCompanion.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Numbers are numbers: no "42" for 42, so the OpenAPI document says integer, not integer-or-string.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddApiDocument();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddWebAppCors(builder.Configuration);
builder.Services.AddApiHealthChecks(builder.Configuration);
builder.Services.AddSignIn(builder.Configuration);
builder.Services.AddFreshness(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiDocument();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapUserRoleEndpoints();
app.MapFreshnessEndpoints();

if (app.Environment.IsDevelopment())
{
    await DevelopmentDatabase.MigrateAndSeedAsync(app.Services);
}

await app.RunAsync();
