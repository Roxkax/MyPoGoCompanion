using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MyPoGoCompanion.Observability.UnitTests;

public class ScrubbingJsonConsoleFormatterTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Writes_one_json_line_without_personal_data()
    {
        var formatter = new ScrubbingJsonConsoleFormatter { Time = new FixedTime(_now) };
        var line = formatter.Format(LogLevel.Warning, "MyPoGoCompanion.Api.Auth.Sessions", new EventId(7, "Reuse"),
            "Sign-in of ash.pt@example.com ended", Thrown(new InvalidOperationException("for ash.pt@example.com")),
            [new("Email", "ash.pt@example.com"), new("Tries", 3), new("{OriginalFormat}", "Sign-in of {Email} ended")]);

        Assert.Single(line.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.Equal(_now, root.GetProperty("time").GetDateTimeOffset());
        Assert.Equal("Warning", root.GetProperty("level").GetString());
        Assert.Equal("MyPoGoCompanion.Api.Auth.Sessions", root.GetProperty("category").GetString());
        Assert.Equal(7, root.GetProperty("eventId").GetInt32());
        Assert.Equal("Sign-in of [email] ended", root.GetProperty("message").GetString());
        Assert.StartsWith("System.InvalidOperationException: for [email]", root.GetProperty("exception").GetString(), StringComparison.Ordinal);
        Assert.Equal("[email]", root.GetProperty("values").GetProperty("Email").GetString());
        Assert.Equal("3", root.GetProperty("values").GetProperty("Tries").GetString());
        Assert.False(root.GetProperty("values").TryGetProperty("{OriginalFormat}", out _));
        Assert.False(root.TryGetProperty("traceId", out _));
        Assert.DoesNotContain("ash.pt", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Adds_the_trace_id_inside_a_request()
    {
        using var activity = new Activity("request").Start();
        var line = new ScrubbingJsonConsoleFormatter().Format(LogLevel.Error, "test", default, "Boom");

        using var json = JsonDocument.Parse(line);
        Assert.Equal(activity.TraceId.ToHexString(), json.RootElement.GetProperty("traceId").GetString());
        Assert.False(json.RootElement.TryGetProperty("values", out _));
    }

    [Fact]
    public void Writes_nothing_for_an_empty_entry()
    {
        var formatter = new ScrubbingJsonConsoleFormatter();
        using var writer = new StringWriter();
        formatter.Write(
            new Microsoft.Extensions.Logging.Abstractions.LogEntry<string>(LogLevel.Information, "test", default, "state", null, (_, _) => null!),
            null, writer);
        Assert.Equal(string.Empty, writer.ToString());
    }

    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
