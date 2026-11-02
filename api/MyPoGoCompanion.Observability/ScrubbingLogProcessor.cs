using OpenTelemetry;
using OpenTelemetry.Logs;

namespace MyPoGoCompanion.Observability;

/// <summary>
/// Removes personal data from every log record before it is sent to Application Insights: the message, every text
/// value and the exception. Runs before the exporter (added first).
/// </summary>
public sealed class ScrubbingLogProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.FormattedMessage = PersonalData.Scrub(data.FormattedMessage);
        data.Body = PersonalData.Scrub(data.Body);
        if (data.Attributes is { Count: > 0 } attributes)
        {
            data.Attributes = attributes.Select(Scrub).ToList();
        }

        if (data.Exception is { } exception)
        {
            data.Exception = RedactedException.From(exception);
        }
    }

    private static KeyValuePair<string, object?> Scrub(KeyValuePair<string, object?> attribute) =>
        attribute.Value is string text ? new(attribute.Key, PersonalData.Scrub(text)) : attribute;
}
