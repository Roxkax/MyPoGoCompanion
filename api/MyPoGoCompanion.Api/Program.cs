using MyPoGoCompanion.Api.Configuration;
using MyPoGoCompanion.Api.Health;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "MyPoGoCompanion API";
    return Task.CompletedTask;
}));
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddWebAppCors(builder.Configuration);
builder.Services.AddApiHealthChecks(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

app.MapOpenApi();
app.MapHealthEndpoints();

if (app.Environment.IsDevelopment())
{
    await DevelopmentDatabase.MigrateAndSeedAsync(app.Services);
}

await app.RunAsync();
