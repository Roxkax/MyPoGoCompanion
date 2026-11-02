using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyPoGoCompanion.Observability.UnitTests;

public class ScrubbingProcessorTests
{
    [Fact]
    public void Spans_lose_the_client_address_and_query_and_personal_data()
    {
        using var source = new ActivitySource("test");
        using var listener = Listen(source.Name);
        using var activity = source.StartActivity("GET /me")!;
        activity.SetTag("client.address", "203.0.113.42");
        activity.SetTag("url.query", "?code=abc");
        activity.SetTag("url.full", "https://accounts.google.com/o/oauth2/token?code=abc");
        activity.SetTag("enduser.id", "0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10");
        activity.SetTag("note", "for ash.pt@example.com");
        activity.SetTag("http.response.status_code", 500);
        activity.SetStatus(ActivityStatusCode.Error, "failed for ash.pt@example.com");

        new ScrubbingActivityProcessor().OnEnd(activity);

        Assert.Null(activity.GetTagItem("client.address"));
        Assert.Null(activity.GetTagItem("url.query"));
        Assert.Equal("https://accounts.google.com/o/oauth2/token", activity.GetTagItem("url.full"));
        Assert.Equal("0f8c2d1e-5b7a-4c39-9e61-2a4d8b3f7c10", activity.GetTagItem("enduser.id"));
        Assert.Equal("for [email]", activity.GetTagItem("note"));
        Assert.Equal(500, activity.GetTagItem("http.response.status_code"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("failed for [email]", activity.StatusDescription);
    }

    [Fact]
    public void A_clean_status_description_stays()
    {
        using var source = new ActivitySource("test-clean");
        using var listener = Listen(source.Name);
        using var activity = source.StartActivity("GET /health")!;
        activity.SetStatus(ActivityStatusCode.Error, "database down");

        new ScrubbingActivityProcessor().OnEnd(activity);

        Assert.Equal("database down", activity.StatusDescription);
    }

    [Theory]
    [InlineData("https://api.example/health", "https://api.example/health")]
    [InlineData("https://api.example/x?y=1", "https://api.example/x")]
    public void Urls_lose_their_query(string url, string expected) => Assert.Equal(expected, ScrubbingActivityProcessor.WithoutQuery(url));

    [Fact]
    public void Log_records_lose_personal_data_before_they_are_exported()
    {
        var exported = new List<(string? Message, string? Body, List<KeyValuePair<string, object?>> Attributes, Exception? Exception)>();
        using (var factory = LoggerFactory.Create(logging => logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.AddProcessor(new ScrubbingLogProcessor());
            options.AddProcessor(new Collect(record => exported.Add(
                (record.FormattedMessage, record.Body, record.Attributes?.ToList() ?? [], record.Exception))));
        })))
        {
            var logger = factory.CreateLogger("test");
#pragma warning disable CA1848, CA2254 // A plain call is what the test needs.
            logger.LogError(new InvalidOperationException("No account for ash.pt@example.com"), "Sign-in failed for {Email} after {Tries} tries", "ash.pt@example.com", 3);
            logger.LogInformation("Nothing personal");
#pragma warning restore CA1848, CA2254
        }

        Assert.Equal(2, exported.Count);
        var (message, _, attributes, exception) = exported[0];
        Assert.Equal("Sign-in failed for [email] after 3 tries", message);
        Assert.Contains(attributes, a => a.Key == "Email" && (string?)a.Value == "[email]");
        Assert.Contains(attributes, a => a.Key == "Tries" && a.Value is 3);
        Assert.IsType<RedactedException>(exception);
        Assert.Equal("Nothing personal", exported[1].Message);
        Assert.Null(exported[1].Exception);
    }

    private static ActivityListener Listen(string name)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private sealed class Collect(Action<LogRecord> onEnd) : BaseProcessor<LogRecord>
    {
        public override void OnEnd(LogRecord data) => onEnd(data);
    }
}
