using System.Text.RegularExpressions;

namespace MyPoGoCompanion.Observability;

/// <summary>
/// Removes personal data from text before it leaves the app as a log or telemetry (decision 2026-11-02): email
/// addresses, tokens (JWTs, bearer tokens, refresh tokens and other long random strings) and IPv4 addresses. A user's
/// random id (a GUID), trace ids (hex) and version numbers stay, so a problem can still be traced.
/// </summary>
public static partial class PersonalData
{
    public const string Email = "[email]";
    public const string Token = "[token]";
    public const string IpAddress = "[ip]";

    /// <summary>The text with personal data replaced by placeholders; null and empty text come back unchanged.</summary>
    public static string? Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var scrubbed = EmailPattern().Replace(text, Email);
        scrubbed = BearerPattern().Replace(scrubbed, "Bearer " + Token);
        scrubbed = JwtPattern().Replace(scrubbed, Token);
        scrubbed = LongTokenPattern().Replace(scrubbed, match => IsHexOrGuid(match.ValueSpan) ? match.Value : Token);
        scrubbed = Ipv4Pattern().Replace(scrubbed, IpAddress);
        return scrubbed;
    }

    /// <summary>Whether <see cref="Scrub"/> would change the text.</summary>
    public static bool Contains(string? text) => !string.Equals(text, Scrub(text), StringComparison.Ordinal);

    /// <summary>Trace ids are hex; user ids are GUIDs (hex and dashes). Random tokens use the whole alphabet.</summary>
    private static bool IsHexOrGuid(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex BearerPattern();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    // Refresh tokens and Google codes are 32+ characters of base64url.
    [GeneratedRegex(@"(?<![A-Za-z0-9_\-])[A-Za-z0-9_\-]{32,}(?![A-Za-z0-9_\-])", RegexOptions.CultureInvariant)]
    private static partial Regex LongTokenPattern();

    // Not after "Version=" (assembly versions in .NET error messages look like addresses).
    [GeneratedRegex(@"(?<![\d.]|Version=)(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)(?![\d.])", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Pattern();
}
