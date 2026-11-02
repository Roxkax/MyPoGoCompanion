using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyPoGoCompanion.Jobs.DailyImport;

/// <summary>
/// The game's text in one language (resource id → text), as datamined into <c>{"data": [key, text, key, text, …]}</c>.
/// The release's texts come first and the server's newer texts replace them. A text can point at another one
/// (<c>&lt;&lt;move_name_0296&gt;&gt;+</c> for a Mega's "Surf+"), resolved when it is read.
/// </summary>
public sealed partial class GameText
{
    private const int MaxDepth = 5;

    private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);

    public int Count => _texts.Count;

    /// <summary>Reads the files in order (later files win); null files are skipped.</summary>
    public static GameText Read(params SourceFile?[] files)
    {
        var text = new GameText();
        foreach (var file in files.OfType<SourceFile>())
        {
            text.Add(file);
        }

        return text;
    }

    /// <summary>The text with its references resolved and spaces trimmed, or null when it is missing or empty.</summary>
    public string? Get(string key) => Resolve(key, 0) is { Length: > 0 } value ? value : null;

    private string? Resolve(string key, int depth)
    {
        if (!_texts.TryGetValue(key, out var value))
        {
            return null;
        }

        if (depth < MaxDepth && value.Contains("<<", StringComparison.Ordinal))
        {
            var broken = false;
            value = Reference().Replace(value, match =>
            {
                var inner = Resolve(match.Groups[1].Value, depth + 1);
                broken |= inner is null;
                return inner ?? "";
            });
            if (broken)
            {
                return null;
            }
        }

        return value.Contains("<<", StringComparison.Ordinal) ? null : value.Trim();
    }

    private void Add(SourceFile file)
    {
        try
        {
            using var document = JsonDocument.Parse(file.Content);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() % 2 != 0)
            {
                throw new ImportException($"The {file.Name} isn't a list of resource ids and texts: its format may have changed.");
            }

            string? key = null;
            foreach (var item in data.EnumerateArray())
            {
                var value = item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.ToString();
                if (key is null)
                {
                    key = value;
                }
                else
                {
                    _texts[key] = value;
                    key = null;
                }
            }
        }
        catch (JsonException exception)
        {
            throw new ImportException($"The {file.Name} isn't the JSON the import expects ({exception.Message}).", exception);
        }
    }

    [GeneratedRegex(@"<<([^<>]+)>>")]
    private static partial Regex Reference();
}
