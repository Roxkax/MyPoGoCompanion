namespace MyPoGoCompanion.Observability;

/// <summary>
/// Stands in for an exception whose message or stack trace contained personal data: the same type name, message and
/// stack trace as text, with the personal data removed. Only created by <see cref="ScrubbingLogProcessor"/>.
/// </summary>
public sealed class RedactedException : Exception
{
    private readonly string? _stackTrace;

    public RedactedException()
    {
    }

    public RedactedException(string message)
        : base(message)
    {
    }

    public RedactedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private RedactedException(string originalType, string message, string? stackTrace)
        : base($"{originalType}: {message}")
    {
        OriginalType = originalType;
        _stackTrace = stackTrace;
    }

    /// <summary>The full name of the exception type that was replaced.</summary>
    public string OriginalType { get; } = typeof(RedactedException).FullName!;

    public override string? StackTrace => _stackTrace;

    public override string ToString() => _stackTrace is null ? Message : $"{Message}{Environment.NewLine}{_stackTrace}";

    /// <summary>The exception itself when it holds no personal data, otherwise a scrubbed stand-in.</summary>
    public static Exception From(Exception exception)
    {
        var text = exception.ToString();
        if (!PersonalData.Contains(text))
        {
            return exception;
        }

        // The inner exceptions' messages are part of ToString's stack trace text, so they're scrubbed there too.
        var type = exception.GetType().FullName ?? exception.GetType().Name;
        var stack = PersonalData.Scrub(text[Math.Min(text.Length, FirstLineLength(text))..].TrimStart('\r', '\n'));
        return new RedactedException(type, PersonalData.Scrub(exception.Message)!, string.IsNullOrEmpty(stack) ? null : stack);
    }

    private static int FirstLineLength(string text)
    {
        var end = text.IndexOf('\n', StringComparison.Ordinal);
        return end < 0 ? text.Length : end;
    }
}
