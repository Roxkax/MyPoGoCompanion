using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace MyPoGoCompanion.Observability.UnitTests;

public class TelemetrySetupTests
{
    private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://example.invalid/";

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(" InstrumentationKey=abc ", "InstrumentationKey=abc")]
    public void Reads_the_connection_string(string? value, string? expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [TelemetrySetup.ConnectionStringSetting] = value })
            .Build();
        Assert.Equal(expected, TelemetrySetup.ConnectionString(configuration));
    }

    [Fact]
    public void Without_application_insights_the_console_keeps_every_level_and_nothing_is_exported()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Development" });
        builder.AddTelemetry("test-job");
        using var host = builder.Build();

        Assert.Null(host.Services.GetService<TracerProvider>());
        Assert.Equal(ScrubbingJsonConsoleFormatter.FormatterName, host.Services.GetRequiredService<IOptions<ConsoleLoggerOptions>>().Value.FormatterName);
        Assert.True(host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("x").IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void With_application_insights_traces_are_exported_and_the_console_keeps_warnings()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
        builder.Configuration[TelemetrySetup.ConnectionStringSetting] = ConnectionString;
        builder.Configuration["APP_VERSION"] = "3cd4b0c";
        var hostTracing = false;
        builder.AddTelemetry("test-job", _ => hostTracing = true);
        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<TracerProvider>());
        Assert.True(hostTracing);
        var console = host.Services.GetRequiredService<IOptions<LoggerFilterOptions>>().Value.Rules
            .Single(rule => rule.ProviderName == typeof(ConsoleLoggerProvider).FullName && rule.Filter is not null);
        Assert.False(console.Filter!(null, null, LogLevel.Information));
        Assert.True(console.Filter!(null, null, LogLevel.Warning));
    }
}
