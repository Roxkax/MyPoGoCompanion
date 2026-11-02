using System.Diagnostics;
using OpenTelemetry;

namespace MyPoGoCompanion.Observability;

/// <summary>
/// Removes personal data from every request, database call and outgoing call (span) before it is sent: the client's
/// IP address and query strings are dropped, and every text value is scrubbed. The user's random id
/// (<c>enduser.id</c>) stays.
/// </summary>
public sealed class ScrubbingActivityProcessor : BaseProcessor<Activity>
{
    /// <summary>Values never sent: who is calling from where, and query strings (which can hold codes or names).</summary>
    internal static readonly HashSet<string> DroppedTags = new(StringComparer.Ordinal)
    {
        "client.address",
        "client.port",
        "http.client_ip",
        "net.peer.ip",
        "network.peer.address",
        "url.query",
        "http.request.header.authorization",
        "http.request.header.cookie",
    };

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var (key, value) in data.TagObjects.ToList())
        {
            if (DroppedTags.Contains(key))
            {
                data.SetTag(key, null);
            }
            else if (value is string text)
            {
                var clean = key is "url.full" or "http.url" or "http.target" ? WithoutQuery(text) : text;
                clean = PersonalData.Scrub(clean);
                if (!string.Equals(clean, text, StringComparison.Ordinal))
                {
                    data.SetTag(key, clean);
                }
            }
        }

        if (data.StatusDescription is { } description && PersonalData.Contains(description))
        {
            data.SetStatus(data.Status, PersonalData.Scrub(description));
        }
    }

    internal static string WithoutQuery(string url)
    {
        var query = url.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? url : url[..query];
    }
}
