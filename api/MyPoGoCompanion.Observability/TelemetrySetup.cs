using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MyPoGoCompanion.Observability;

/// <summary>
/// Telemetry for the API and the scheduled jobs (decision 2026-11-02, docs/OBSERVABILITY.md). With
/// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> set (the deploys set it), requests, database calls, outgoing calls,
/// errors and logs go to that copy's Application Insights, with personal data removed first. Console logs are JSON
/// lines, scrubbed the same way; with Application Insights on, the console keeps only warnings and errors (they would
/// otherwise be stored twice in the log workspace, against the free 5 GB a month).
/// </summary>
public static class TelemetrySetup
{
    public const string ConnectionStringSetting = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>The app's own logs and spans (e.g. web app error reports) use this source name.</summary>
    public const string SourceName = "MyPoGoCompanion";

    /// <summary>
    /// Adds telemetry for <paramref name="serviceName"/> (the role name in Application Insights, e.g. "api").
    /// <paramref name="tracing"/> adds the host's own instrumentation (ASP.NET Core for the API).
    /// </summary>
    public static IHostApplicationBuilder AddTelemetry(
        this IHostApplicationBuilder builder, string serviceName, Action<TracerProviderBuilder>? tracing = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var connectionString = ConnectionString(builder.Configuration);
        var version = builder.Configuration["APP_VERSION"] is { Length: > 0 } v ? v : "dev";

        builder.Logging.AddConsole(console => console.FormatterName = ScrubbingJsonConsoleFormatter.FormatterName)
            .AddConsoleFormatter<ScrubbingJsonConsoleFormatter, ConsoleFormatterOptions>();

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: version)
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]));

        if (connectionString is null)
        {
            return builder;
        }

        builder.Logging.AddFilter<ConsoleLoggerProvider>(level => level >= LogLevel.Warning);
        telemetry
            .WithTracing(traces =>
            {
                traces.AddSource(SourceName).AddHttpClientInstrumentation().AddNpgsql();
                tracing?.Invoke(traces);
                traces.AddProcessor<ScrubbingActivityProcessor>()
                    .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString);
            })
            .WithLogging(
                logs => logs.AddProcessor<ScrubbingLogProcessor>()
                    .AddAzureMonitorLogExporter(options => options.ConnectionString = connectionString),
                options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = false;
                });

        return builder;
    }

    /// <summary>The Application Insights connection string, or null when telemetry isn't sent (local, tests).</summary>
    public static string? ConnectionString(IConfiguration configuration) =>
        configuration[ConnectionStringSetting] is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
