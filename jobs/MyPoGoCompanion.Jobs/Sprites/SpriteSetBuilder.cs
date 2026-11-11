using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using SkiaSharp;

namespace MyPoGoCompanion.Jobs.Sprites;

/// <summary>What one <see cref="SpriteSetBuilder.Build"/> made.</summary>
/// <param name="Pictures">WebP files written.</param>
/// <param name="LeftOut">Female variants and shiny costume pictures, not used by the app.</param>
/// <param name="Unreadable">Files that aren't a picture with something on it (named in <paramref name="Problems"/>).</param>
public sealed record SpriteSetResult(int Pictures, int LeftOut, int Unreadable, IReadOnlyList<string> Problems);

/// <summary>
/// Turns PokeMiners' Pokémon pictures (<c>Images/Pokemon - 256x256/Addressable Assets</c>, <c>pm37.fALOLA.icon.png</c>)
/// into the web app's (docs/SPRITES.md, decision 2026-11-07): the empty border cropped, the picture centred on a
/// square, at most <see cref="MaxSide"/> pixels, saved as WebP (<c>pm37.fALOLA.webp</c>), so every Pokémon fills its
/// box equally (design conventions). Shiny costume pictures (<c>.cFALL_2018.s</c>) and female variants (<c>.g2</c>) are left
/// out: the app has no rows for them. Also writes <c>set.json</c> naming the set (PokeMiners' commit).
/// For the MVP the web deploys run it and ship the folder with the web app; after the MVP the daily import will run
/// it and write to Blob storage instead.
/// </summary>
public static class SpriteSetBuilder
{
    /// <summary>The largest side kept: twice the biggest box the screens draw (about 120 px).</summary>
    public const int MaxSide = 256;

    /// <summary>WebP quality: about 6 KB a picture with no visible loss at screen size.</summary>
    public const int Quality = 85;

    /// <summary>Alpha at or below this counts as empty when cropping (soft shadows and stray pixels).</summary>
    public const byte EmptyAlpha = 8;

    public const string SetFile = "set.json";

    private const string Suffix = ".icon.png";

    public static SpriteSetResult Build(string source, string output, string set, int parallelism = 0)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        ArgumentException.ThrowIfNullOrEmpty(output);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"No picture folder at {source}.");
        }

        Directory.CreateDirectory(output);
        var files = Directory.EnumerateFiles(source, "*" + Suffix).Order(StringComparer.Ordinal).ToList();
        var wanted = files.Where(file => IsWanted(Path.GetFileName(file))).ToList();
        var problems = new ConcurrentBag<string>();
        var written = 0;
        Parallel.ForEach(
            wanted,
            new ParallelOptions { MaxDegreeOfParallelism = parallelism > 0 ? parallelism : Environment.ProcessorCount },
            file =>
            {
                var name = Path.GetFileName(file)[..^Suffix.Length];
                var webp = Convert(File.ReadAllBytes(file));
                if (webp is null)
                {
                    problems.Add(Path.GetFileName(file));
                    return;
                }

                File.WriteAllBytes(Path.Combine(output, name + ".webp"), webp);
                Interlocked.Increment(ref written);
            });

        var info = new Dictionary<string, object>
        {
            ["set"] = set,
            ["pictures"] = written,
            ["built"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
        };
        File.WriteAllText(Path.Combine(output, SetFile), JsonSerializer.Serialize(info));
        return new SpriteSetResult(written, files.Count - wanted.Count, problems.Count, [.. problems.Order(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// A Pokémon picture the app uses: not a female variant (<c>.g2</c>) nor a costume's shiny picture
    /// (<c>.c….s</c>; shiny costumes aren't shown, decision 2026-11-08).
    /// </summary>
    public static bool IsWanted(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (!fileName.StartsWith("pm", StringComparison.Ordinal) || !fileName.EndsWith(Suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = fileName[..^Suffix.Length].Split('.');
        var costume = parts.Skip(1).Any(part => part.Length > 1 && part[0] == 'c' && char.IsAsciiLetterUpper(part[1]));
        return !parts.Skip(1).Any(part => part == "g2") && !(costume && parts[^1] == "s");
    }

    /// <summary>
    /// One picture: cropped to what isn't (nearly) transparent, centred on a transparent square, scaled down to
    /// <see cref="MaxSide"/> when bigger, as WebP. Null when the bytes aren't a picture or nothing is on it.
    /// </summary>
    public static byte[]? Convert(byte[] png)
    {
        // SKBitmap.Decode throws on bytes no codec reads; asking for the codec first returns null instead.
        using var stream = new SKMemoryStream(png);
        using var codec = SKCodec.Create(stream);
        using var decoded = codec is null ? null : SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        using var bitmap = decoded.ColorType == SKColorType.Rgba8888 ? decoded.Copy() : decoded.Copy(SKColorType.Rgba8888);
        if (bitmap is null || Content(bitmap) is not { } box)
        {
            return null;
        }

        var side = Math.Max(box.Width, box.Height);
        var target = Math.Min(side, MaxSide);
        var scale = (float)target / side;
        using var surface = SKSurface.Create(new SKImageInfo(target, target, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        var width = box.Width * scale;
        var height = box.Height * scale;
        var destination = SKRect.Create((target - width) / 2, (target - height) / 2, width, height);
        canvas.DrawImage(image, box, destination, new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var snapshot = surface.Snapshot();
        using var data = snapshot.Encode(SKEncodedImageFormat.Webp, Quality);
        return data?.ToArray();
    }

    /// <summary>The smallest rectangle holding every pixel with alpha above <see cref="EmptyAlpha"/>; null when none.</summary>
    internal static SKRectI? Content(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        int width = bitmap.Width, height = bitmap.Height, rowBytes = bitmap.RowBytes;
        int left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++)
        {
            var row = pixels.Slice(y * rowBytes, width * 4);
            for (var x = 0; x < width; x++)
            {
                if (row[(x * 4) + 3] > EmptyAlpha)
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        return right < 0 ? null : new SKRectI(left, top, right + 1, bottom + 1);
    }
}
