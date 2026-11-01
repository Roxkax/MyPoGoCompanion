using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using MyPoGoCompanion.Api.Health;
using MyPoGoCompanion.Api.OpenApi;

namespace MyPoGoCompanion.Api.UnitTests.OpenApi;

public class OpenApiSetupTests
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void Describe_SetsTitleAndBearerScheme_AndDropsTheServerAddress()
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "MyPoGoCompanion.Api | v1" },
            Servers = [new OpenApiServer { Url = "http://localhost:5099/" }],
        };

        OpenApiSetup.Describe(document);

        Assert.Equal("MyPoGoCompanion API", document.Info.Title);
        Assert.Contains("not affiliated with Niantic", document.Info.Description, StringComparison.Ordinal);
        Assert.Empty(document.Servers);
        var bearer = Assert.IsType<OpenApiSecurityScheme>(document.Components!.SecuritySchemes![OpenApiSetup.BearerScheme]);
        Assert.Equal(SecuritySchemeType.Http, bearer.Type);
        Assert.Equal("bearer", bearer.Scheme);
    }

    [Fact]
    public void SortResponses_PutsStatusCodesInOrder()
    {
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["429"] = new OpenApiResponse(),
                ["401"] = new OpenApiResponse(),
                ["200"] = new OpenApiResponse(),
                ["400"] = new OpenApiResponse(),
            },
        };

        OpenApiSetup.SortResponses(operation);

        Assert.Equal(["200", "400", "401", "429"], operation.Responses.Keys);
    }

    [Fact]
    public void SortResponses_NoneOrOne_LeavesThemAlone()
    {
        var none = new OpenApiOperation();
        var one = new OpenApiOperation { Responses = new OpenApiResponses { ["204"] = new OpenApiResponse() } };

        OpenApiSetup.SortResponses(none);
        OpenApiSetup.SortResponses(one);

        Assert.Empty(none.Responses!);
        Assert.Equal(["204"], one.Responses.Keys);
    }

    [Fact]
    public void TidyEnum_DropsNullAndSaysString()
    {
        var schema = new OpenApiSchema { Enum = [JsonValue.Create("User"), JsonValue.Create("Admin"), null!] };

        OpenApiSetup.TidyEnum(schema);

        Assert.Equal(["User", "Admin"], schema.Enum.Select(value => value.GetValue<string>()));
        Assert.Equal(JsonSchemaType.String, schema.Type);
    }

    [Fact]
    public void TidyEnum_NotAStringEnum_LeavesItAlone()
    {
        var plain = new OpenApiSchema { Type = JsonSchemaType.Object };
        var numbers = new OpenApiSchema { Enum = [JsonValue.Create(1), JsonValue.Create(2)], Type = JsonSchemaType.Integer };

        OpenApiSetup.TidyEnum(plain);
        OpenApiSetup.TidyEnum(numbers);

        Assert.Null(plain.Enum);
        Assert.Equal(JsonSchemaType.Object, plain.Type);
        Assert.Equal(2, numbers.Enum.Count);
        Assert.Equal(JsonSchemaType.Integer, numbers.Type);
    }

    [Fact]
    public void AddExample_KnownType_AddsItAsTheApiWritesIt()
    {
        var schema = new OpenApiSchema();

        OpenApiSetup.AddExample(schema, typeof(HealthResponse), _json);

        var example = Assert.Single(schema.Examples!);
        Assert.Equal("Healthy", example!["status"]!.GetValue<string>());
        Assert.Equal("Healthy", example["checks"]!["database"]!.GetValue<string>());
    }

    [Fact]
    public void AddProblemExamples_EachErrorShowsItsOwnStatus()
    {
        var problem = () => new OpenApiResponse
        {
            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() },
        };
        var operation = new OpenApiOperation
        {
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() } },
                ["401"] = problem(),
                ["429"] = problem(),
                ["418"] = problem(),
            },
        };

        OpenApiSetup.AddProblemExamples(operation, _json);

        var content = (string status) => ((OpenApiResponse)operation.Responses[status]).Content!;
        Assert.Null(content("200")["application/json"].Example);
        Assert.Equal(401, content("401")["application/problem+json"].Example!["status"]!.GetValue<int>());
        Assert.Equal("Too Many Requests", content("429")["application/problem+json"].Example!["title"]!.GetValue<string>());
        Assert.Null(content("418")["application/problem+json"].Example);
    }

    [Fact]
    public void AddExample_TypeWithoutOne_AddsNothing()
    {
        var schema = new OpenApiSchema();

        OpenApiSetup.AddExample(schema, typeof(string), _json);

        Assert.Null(schema.Examples);
    }

    [Fact]
    public void Examples_EveryOneSerializesToAnObject()
    {
        foreach (var type in OpenApiExamples.Types)
        {
            var node = JsonSerializer.SerializeToNode(OpenApiExamples.For(type), type, _json);
            Assert.IsType<JsonObject>(node);
        }

        Assert.Contains(typeof(ProblemDetails), OpenApiExamples.Types);
    }

    /// <summary>A tiny app with the API's document set-up: one open endpoint and one that needs sign-in.</summary>
    private static WebApplication App(bool docsEnabled)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration["ApiDocs:Enabled"] = docsEnabled ? "true" : "false";
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddAuthorization();
        builder.Services.AddApiDocument();
        var app = builder.Build();
        app.MapGet("/open", () => TypedResults.Ok(OpenApiExamples.For(typeof(HealthResponse)) as HealthResponse))
            .WithName("Open")
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        app.MapGet("/closed", () => TypedResults.NoContent())
            .WithName("Closed")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        app.MapApiDocument();
        return app;
    }

    /// <summary>The document as the API builds it (the app has to start: its endpoints join the document then).</summary>
    private static async Task<OpenApiDocument> DocumentAsync(WebApplication app)
    {
        await app.StartAsync(TestContext.Current.CancellationToken);
        return await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1")
            .GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AddApiDocument_DescribesTokenNeedsExamplesAndEnums()
    {
        await using var app = App(docsEnabled: false);

        var document = await DocumentAsync(app);

        Assert.Equal("MyPoGoCompanion API", document.Info.Title);
        Assert.Empty(document.Servers ?? []);
        Assert.Equal(["/closed", "/open"], document.Paths.Keys.Order(StringComparer.Ordinal));
        var closed = document.Paths["/closed"].Operations![HttpMethod.Get];
        var open = document.Paths["/open"].Operations![HttpMethod.Get];
        Assert.Single(closed.Security!);
        Assert.Null(open.Security);
        Assert.Equal(["200", "429"], open.Responses!.Keys);
        var health = Assert.IsType<OpenApiSchema>(document.Components!.Schemas!["HealthResponse"]);
        Assert.Equal("3cd4b0c", Assert.Single(health.Examples!)!["version"]!.GetValue<string>());
        var status = Assert.IsType<OpenApiSchema>(document.Components.Schemas[nameof(HealthStatus)]);
        Assert.Equal(JsonSchemaType.String, status.Type);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapApiDocument_DocsPageOnlyWhenSwitchedOn(bool enabled)
    {
        await using var app = App(enabled);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var page = await client.GetAsync(new Uri("/docs/index.html", UriKind.Relative), TestContext.Current.CancellationToken);
        var document = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(enabled ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound, page.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, document.StatusCode);
    }
}
