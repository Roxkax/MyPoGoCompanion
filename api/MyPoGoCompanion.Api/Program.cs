using System.Text.Json.Serialization;
using MyPoGoCompanion.Api.Admin;
using MyPoGoCompanion.Api.Auth;
using MyPoGoCompanion.Api.Configuration;
using MyPoGoCompanion.Api.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "MyPoGoCompanion API";
    return Task.CompletedTask;
}));
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddWebAppCors(builder.Configuration);
builder.Services.AddApiHealthChecks(builder.Configuration);
builder.Services.AddSignIn(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapUserRoleEndpoints();

if (app.Environment.IsDevelopment())
{
    await DevelopmentDatabase.MigrateAndSeedAsync(app.Services);
}

await app.RunAsync();
