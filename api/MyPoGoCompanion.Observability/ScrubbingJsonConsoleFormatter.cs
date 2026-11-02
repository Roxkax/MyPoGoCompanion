using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace MyPoGoCompanion.Observability;

/// <summary>
/// Console logs as one JSON object per line (Container Apps keeps them in the log workspace), with personal data
/// removed: <c>{"time":…,"level":…,"category":…,"eventId":…,"message":…,"exception":…,"traceId":…,"values":{…}}</c>.
/// </summary>
public sealed class ScrubbingJsonConsoleFormatter() : ConsoleFormatter(FormatterName)
{
    public const string FormatterName = "scrubbed-json";

    private static readonly JsonWriterOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The clock, for tests.</summary>
    internal TimeProvider Time { get; init; } = TimeProvider.System;

    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (message is null && logEntry.Exception is null)
        {
            return;
        }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer, _options))
        {
            json.WriteStartObject();
            json.WriteString("time", Time.GetUtcNow());
            json.WriteString("level", logEntry.LogLevel.ToString());
            json.WriteString("category", logEntry.Category);
            json.WriteNumber("eventId", logEntry.EventId.Id);
            json.WriteString("message", PersonalData.Scrub(message));
            if (logEntry.Exception is { } exception)
            {
                json.WriteString("exception", RedactedException.From(exception).ToString());
            }

            if (System.Diagnostics.Activity.Current is { } activity)
            {
                json.WriteString("traceId", activity.TraceId.ToHexString());
            }

            if (logEntry.State is IReadOnlyList<KeyValuePair<string, object?>> values && values.Count > 0)
            {
                json.WriteStartObject("values");
                foreach (var (key, value) in values)
                {
                    if (key != "{OriginalFormat}")
                    {
                        json.WriteString(key, PersonalData.Scrub(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }

                json.WriteEndObject();
            }

            json.WriteEndObject();
        }

        textWriter.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
    }

    /// <summary>For tests: formats one entry the way the console logger would.</summary>
    internal string Format(LogLevel level, string category, EventId eventId, string message, Exception? exception = null,
        IReadOnlyList<KeyValuePair<string, object?>>? values = null)
    {
        using var writer = new StringWriter();
        IReadOnlyList<KeyValuePair<string, object?>> state = values ?? [];
        Write(new LogEntry<IReadOnlyList<KeyValuePair<string, object?>>>(level, category, eventId, state, exception, (_, _) => message),
            scopeProvider: null, writer);
        return writer.ToString();
    }
}
