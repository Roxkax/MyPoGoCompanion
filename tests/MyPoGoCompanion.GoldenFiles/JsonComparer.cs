using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MyPoGoCompanion.GoldenFiles;

/// <summary>Compares two JSON documents and lists where they differ.</summary>
/// <remarks>
/// Objects compare by property name (order ignored), arrays item by item. Long strings that
/// differ are shown around the first differing character, so a 1,950-character search string
/// stays readable.
/// </remarks>
public static partial class JsonComparer
{
    private const int StringContext = 30;
    private const int ValueLength = 80;

    /// <summary>The differences between <paramref name="expected"/> and <paramref name="actual"/>.</summary>
    /// <returns>Up to <see cref="JsonCompareOptions.MaxDifferences"/> differences, and whether there were more.</returns>
    public static JsonComparison Compare(JsonNode? expected, JsonNode? actual, JsonCompareOptions? options = null)
    {
        options ??= JsonCompareOptions.Exact;
        var differences = new List<JsonDifference>();
        var truncated = !Walk(expected, actual, "$", options, differences);
        return new JsonComparison(differences, truncated);
    }

    /// <returns><c>false</c> once <see cref="JsonCompareOptions.MaxDifferences"/> is reached.</returns>
    private static bool Walk(JsonNode? expected, JsonNode? actual, string path, JsonCompareOptions options,
        List<JsonDifference> differences)
    {
        bool Add(string description)
        {
            if (differences.Count >= options.MaxDifferences)
            {
                return false;
            }

            differences.Add(new JsonDifference(path, description));
            return true;
        }

        var expectedKind = Kind(expected);
        var actualKind = Kind(actual);
        if (expectedKind != actualKind)
        {
            return Add($"expected {Describe(expected)}, found {Describe(actual)}");
        }

        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var (name, value) in e)
                {
                    if (!a.ContainsKey(name) && !Add($"missing {Property(name)} (expected {Describe(value)})"))
                    {
                        return false;
                    }
                }

                foreach (var (name, value) in a)
                {
                    if (!e.ContainsKey(name) && !Add($"unexpected {Property(name)} = {Describe(value)}"))
                    {
                        return false;
                    }
                }

                foreach (var (name, value) in e)
                {
                    if (a.TryGetPropertyValue(name, out var other)
                        && !Walk(value, other, path + Segment(name), options, differences))
                    {
                        return false;
                    }
                }

                return true;

            case (JsonArray e, JsonArray a):
                if (e.Count != a.Count && !Add($"{e.Count} items expected, {a.Count} found"))
                {
                    return false;
                }

                for (var i = 0; i < Math.Min(e.Count, a.Count); i++)
                {
                    if (!Walk(e[i], a[i], $"{path}[{i}]", options, differences))
                    {
                        return false;
                    }
                }

                return true;

            case (JsonValue e, JsonValue a):
                var description = CompareValues(e, a, expectedKind, options);
                return description is null || Add(description);

            default:
                return true; // both null
        }
    }

    private static string? CompareValues(JsonValue expected, JsonValue actual, JsonValueKind kind,
        JsonCompareOptions options)
    {
        switch (kind)
        {
            case JsonValueKind.Number:
                var e = ToDouble(expected);
                var a = ToDouble(actual);
                var allowed = options.RelativeTolerance * Math.Max(Math.Abs(e), Math.Abs(a));
                return e == a || Math.Abs(e - a) <= allowed
                    ? null
                    : $"expected {Number(e)}, found {Number(a)} (difference {Number(a - e)})";

            case JsonValueKind.String:
                var expectedText = expected.GetValue<string>();
                var actualText = actual.GetValue<string>();
                return expectedText == actualText ? null : DescribeStrings(expectedText, actualText);

            default: // true / false
                return expected.GetValueKind() == actual.GetValueKind()
                    ? null
                    : $"expected {Describe(expected)}, found {Describe(actual)}";
        }
    }

    private static double ToDouble(JsonValue value) =>
        double.Parse(value.ToJsonString(), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static string DescribeStrings(string expected, string actual)
    {
        if (expected.Length <= ValueLength && actual.Length <= ValueLength)
        {
            return $"expected {Quote(expected)}, found {Quote(actual)}";
        }

        var at = 0;
        while (at < expected.Length && at < actual.Length && expected[at] == actual[at])
        {
            at++;
        }

        return $"strings differ at character {at} (lengths {expected.Length} and {actual.Length})"
            + $"{Environment.NewLine}    expected {Excerpt(expected, at)}"
            + $"{Environment.NewLine}    found    {Excerpt(actual, at)}";
    }

    private static string Excerpt(string text, int at)
    {
        var start = Math.Max(0, at - StringContext);
        var end = Math.Min(text.Length, at + StringContext);
        return (start > 0 ? "…" : "") + Quote(text[start..end]) + (end < text.Length ? "…" : "");
    }

    private static JsonValueKind Kind(JsonNode? node) => node switch
    {
        null => JsonValueKind.Null,
        JsonValue value => value.GetValueKind() is JsonValueKind.False ? JsonValueKind.True : value.GetValueKind(),
        _ => node.GetValueKind(),
    };

    private static string Describe(JsonNode? node)
    {
        var text = node?.ToJsonString() ?? "null";
        var kind = Kind(node) switch
        {
            JsonValueKind.Object => "object ",
            JsonValueKind.Array => "array ",
            _ => "",
        };
        return kind + (text.Length <= ValueLength ? text : string.Concat(text.AsSpan(0, ValueLength - 1), "…"));
    }

    private static string Quote(string text) => JsonSerializer.Serialize(text, _quoteOptions);

    private static readonly JsonSerializerOptions _quoteOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string Property(string name) => $"property {Quote(name)}";

    private static string Segment(string name) => Identifier().IsMatch(name) ? "." + name : $"[{Quote(name)}]";

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex Identifier();
}
