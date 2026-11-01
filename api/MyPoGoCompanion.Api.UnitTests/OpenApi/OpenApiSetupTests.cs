using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
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
}
