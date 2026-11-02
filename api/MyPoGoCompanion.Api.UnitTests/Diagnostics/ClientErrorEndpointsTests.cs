using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Api.Diagnostics;

namespace MyPoGoCompanion.Api.UnitTests.Diagnostics;

public class ClientErrorEndpointsTests
{
    private static ClientErrorRequest Request(
        string? reference = "7f3a2c91", ClientErrorKind? kind = ClientErrorKind.Screen, string? message = "Boom",
        string? stack = "at X", string? path = "/pokedex", string? version = "3cd4b0c") =>
        new(reference, kind, message, stack, path, version);

    [Fact]
    public void A_report_is_logged_as_an_error_with_its_reference()
    {
        var loggers = new RecordingLoggers();

        var result = ClientErrorEndpoints.Report(Request(), loggers);

        Assert.IsType<Accepted>(result.Result);
        var entry = Assert.Single(loggers.Entries);
        Assert.Equal("MyPoGoCompanion.WebApp", entry.Category);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Web app error 7f3a2c91 (Screen) on /pokedex, web version 3cd4b0c: Boom\nat X", entry.Message);
    }

    [Fact]
    public void Missing_values_get_defaults_and_long_ones_are_cut()
    {
        var loggers = new RecordingLoggers();

        ClientErrorEndpoints.Report(
            Request(reference: null, kind: null, message: "  " + new string('m', 2_000), stack: null, path: "/auth/callback?code=secret#x", version: null),
            loggers);

        var message = Assert.Single(loggers.Entries).Message;
        Assert.StartsWith("Web app error none (Script) on /auth/callback, web version unknown: mmm", message, StringComparison.Ordinal);
        Assert.Contains("…\n(no stack trace)", message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void A_report_without_a_message_is_refused(string? message)
    {
        var result = ClientErrorEndpoints.Report(Request(message: message), new RecordingLoggers());
        var problem = Assert.IsType<ProblemHttpResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
    }

    [Theory]
    [InlineData("7f3a")]
    [InlineData("7F3A2C91")]
    [InlineData("not-hex-at-all")]
    [InlineData("4bf92f3577b34da6a3ce929d0e0e47361")]
    public void A_reference_that_is_not_short_hex_is_refused(string reference)
    {
        var result = ClientErrorEndpoints.Report(Request(reference: reference), new RecordingLoggers());
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ProblemHttpResult>(result.Result).StatusCode);
    }

    [Theory]
    [InlineData(null, "unknown")]
    [InlineData(" ", "unknown")]
    [InlineData("/pokedex/mewtwo", "/pokedex/mewtwo")]
    [InlineData("/battle?type=fire", "/battle")]
    [InlineData("/x#top", "/x")]
    public void Only_the_path_is_kept(string? path, string expected) => Assert.Equal(expected, ClientErrorEndpoints.CleanPath(path));

    [Fact]
    public void Paths_are_cut_too() =>
        Assert.Equal(ClientErrorEndpoints.MaxPathLength + 1, ClientErrorEndpoints.CleanPath("/" + new string('a', 500)).Length);

    [Fact]
    public void The_endpoint_is_mapped_with_its_rate_limit()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddClientErrors();
        using var app = builder.Build();

        app.MapClientErrorEndpoints();

        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>());
        Assert.Equal("/client-errors", endpoint.RoutePattern.RawText);
        Assert.Equal(ClientErrorEndpoints.RateLimitPolicy, endpoint.Metadata.GetRequiredMetadata<EnableRateLimitingAttribute>().PolicyName);
        Assert.NotNull(app.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value);
    }

    private sealed class RecordingLoggers : ILoggerFactory
    {
        public List<(string Category, LogLevel Level, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Entries);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class Logger(string category, List<(string, LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Add((category, logLevel, formatter(state, exception)));
        }
    }
}
