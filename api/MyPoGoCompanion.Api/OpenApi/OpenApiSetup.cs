using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace MyPoGoCompanion.Api.OpenApi;

/// <summary>
/// The OpenAPI document (<c>/openapi/v1.json</c>, committed as <c>api/openapi.json</c>) that the web app's client and,
/// later, the mobile apps' clients are generated from, and the interactive docs page on QA and local copies.
/// </summary>
internal static class OpenApiSetup
{
    public const string BearerScheme = "Bearer";

    /// <summary>Where the interactive docs page is served when <c>ApiDocs:Enabled</c> is true.</summary>
    public const string DocsPath = "/docs";

    internal const string Description =
        "The shared API behind MyPoGoCompanion's web app (and, later, its mobile apps). " +
        "Unofficial fan project, not affiliated with Niantic, Nintendo or The Pokémon Company.\n\n" +
        "**Signing in:** the web app sends people to Google, then swaps Google's one-time code for our own tokens with " +
        "`POST /auth/google`. Send the access token as `Authorization: Bearer <token>`; it lasts 15 minutes, and " +
        "`POST /auth/refresh` swaps the refresh token for the next pair.\n\n" +
        "**Errors** come as problem details (`application/problem+json`) with a `detail` that can be shown to people.";

    public static IServiceCollection AddApiDocument(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                Describe(document);
                return Task.CompletedTask;
            });
            options.AddOperationTransformer((operation, context, _) =>
            {
                RequireBearer(operation, context);
                SortResponses(operation);
                return Task.CompletedTask;
            });
            options.AddSchemaTransformer((schema, context, _) =>
            {
                var json = context.ApplicationServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
                AddExample(schema, context.JsonTypeInfo.Type, json.Value.SerializerOptions);
                TidyEnum(schema);
                return Task.CompletedTask;
            });
        });
        return services;
    }

    /// <summary>The document everywhere; the interactive docs page only where <c>ApiDocs:Enabled</c> is true (QA and local).</summary>
    public static WebApplication MapApiDocument(this WebApplication app)
    {
        app.MapOpenApi();
        if (app.Configuration.GetValue<bool>("ApiDocs:Enabled"))
        {
            // Scalar's page, served from the package (no CDN). Its cloud extras (AI agent, MCP, telemetry) stay off.
            app.MapScalarApiReference(DocsPath, options => options
                .WithTitle("MyPoGoCompanion API")
                .AddPreferredSecuritySchemes(BearerScheme)
                .ShowOperationId()
                .HideDeveloperTools()
                .DisableAgent()
                .DisableMcp()
                .DisableTelemetry());
        }

        return app;
    }

    /// <summary>Title, description and the bearer scheme. No server address, so the document is the same everywhere.</summary>
    internal static void Describe(OpenApiDocument document)
    {
        document.Info.Title = "MyPoGoCompanion API";
        document.Info.Description = Description;
        document.Servers?.Clear();
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "The access token from POST /auth/google or POST /auth/refresh (15 minutes).",
        };
    }

    /// <summary>Endpoints that need a signed-in user say so, so generated clients and the docs page send the token.</summary>
    internal static void RequireBearer(OpenApiOperation operation, OpenApiOperationTransformerContext context)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (!metadata.OfType<IAuthorizeData>().Any() || metadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = [],
        });
    }

    /// <summary>Responses in status-code order (group-wide ones such as 401 would otherwise come first).</summary>
    internal static void SortResponses(OpenApiOperation operation)
    {
        if (operation.Responses is not { Count: > 1 } responses)
        {
            return;
        }

        var sorted = responses.OrderBy(response => response.Key, StringComparer.Ordinal).ToList();
        responses.Clear();
        foreach (var (status, response) in sorted)
        {
            responses.Add(status, response);
        }
    }

    /// <summary>A realistic example for every request and response type (<see cref="OpenApiExamples"/>).</summary>
    internal static void AddExample(OpenApiSchema schema, Type type, JsonSerializerOptions options)
    {
        if (OpenApiExamples.For(type) is { } example)
        {
            schema.Examples = [JsonSerializer.SerializeToNode(example, type, options)!];
        }
    }

    /// <summary>
    /// Enums are strings (the API writes their names). A nullable enum property otherwise puts <c>null</c> into the shared
    /// enum's values; the property itself still says it can be null.
    /// </summary>
    internal static void TidyEnum(OpenApiSchema schema)
    {
        if (schema.Enum is not { Count: > 0 } values)
        {
            return;
        }

        var names = values.Where(value => value is JsonValue v && v.GetValueKind() == JsonValueKind.String).ToList();
        if (names.Count == 0)
        {
            return;
        }

        schema.Enum = names;
        schema.Type = JsonSchemaType.String;
    }
}
