using System.Text.Json;
using MyPoGoCompanion.Jobs.Sprites;
using SkiaSharp;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>Turning PokeMiners' pictures into the web app's (docs/SPRITES.md), on pictures drawn by the test.</summary>
public sealed class SpriteSetBuilderTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("sprites-test-").FullName;

    private string Source => Path.Combine(_folder, "source");

    private string Output => Path.Combine(_folder, "out");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>A transparent PNG of <paramref name="size"/> with an opaque red rectangle at <paramref name="content"/>.</summary>
    private static byte[] Png(int width, int height, SKRectI? content)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(bitmap))
        {
            // A faint pixel in a corner: cropping ignores what is nearly transparent.
            using var faint = new SKPaint { Color = new SKColor(0, 0, 0, SpriteSetBuilder.EmptyAlpha) };
            canvas.DrawPoint(0, 0, faint);
            if (content is { } box)
            {
                using var paint = new SKPaint { Color = SKColors.Red };
                canvas.DrawRect(box, paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private void Write(string name, byte[] bytes)
    {
        Directory.CreateDirectory(Source);
        File.WriteAllBytes(Path.Combine(Source, name), bytes);
    }

    private SKBitmap Read(string name) => SKBitmap.Decode(Path.Combine(Output, name))!;

    [Theory]
    [InlineData("pm6.icon.png", true)]
    [InlineData("pm6.s.icon.png", true)]
    [InlineData("pm6.fMEGA_X.icon.png", true)]
    [InlineData("pm6.fCOSTUME_2020.icon.png", true)]
    [InlineData("pm25.cFALL_2018.icon.png", false)]
    [InlineData("pm25.cFALL_2018.s.icon.png", false)]
    [InlineData("pm3.g2.icon.png", false)]
    [InlineData("pm3.g2.s.icon.png", false)]
    [InlineData("pm115.fMEGA.portrait.png", false)]
    [InlineData("pokemon_icon_001_00.png", false)]
    public void OnlyPicturesTheAppUses_AreKept(string name, bool wanted) => Assert.Equal(wanted, SpriteSetBuilder.IsWanted(name));

    [Fact]
    public void APicture_IsCroppedToItsContent_AndCentredOnASquare()
    {
        var webp = SpriteSetBuilder.Convert(Png(256, 256, new SKRectI(40, 100, 120, 140)))!;

        using var picture = SKBitmap.Decode(webp);
        Assert.Equal((80, 80), (picture.Width, picture.Height));
        // 80 × 40 of red in the middle of the square, transparent above and below.
        Assert.Equal(0, picture.GetPixel(40, 5).Alpha);
        Assert.True(picture.GetPixel(40, 40).Red > 200);
        Assert.Equal(0, picture.GetPixel(40, 75).Alpha);
    }

    [Fact]
    public void APictureBiggerThanTheMaximum_IsScaledDown()
    {
        var webp = SpriteSetBuilder.Convert(Png(512, 400, new SKRectI(0, 0, 512, 300)))!;

        using var picture = SKBitmap.Decode(webp);
        Assert.Equal((SpriteSetBuilder.MaxSide, SpriteSetBuilder.MaxSide), (picture.Width, picture.Height));
    }

    [Fact]
    public void AnEmptyPictureOrSomethingElse_GivesNothing()
    {
        Assert.Null(SpriteSetBuilder.Convert(Png(64, 64, content: null)));
        Assert.Null(SpriteSetBuilder.Convert("not a picture"u8.ToArray()));
    }

    [Fact]
    public async Task Build_WritesEveryWantedPicture_AsWebP_AndNamesTheSet()
    {
        Write("pm6.icon.png", Png(256, 256, new SKRectI(10, 10, 200, 240)));
        Write("pm6.s.icon.png", Png(256, 256, new SKRectI(10, 10, 200, 240)));
        Write("pm25.cFALL_2018.icon.png", Png(64, 64, new SKRectI(1, 1, 60, 60)));
        Write("pm9.icon.png", Png(64, 64, content: null));
        Write("readme.txt", "not a picture"u8.ToArray());

        var result = SpriteSetBuilder.Build(Source, Output, "db10df75", parallelism: 2);

        Assert.Equal((2, 1, 1), (result.Pictures, result.LeftOut, result.Unreadable));
        Assert.Equal(["pm9.icon.png"], result.Problems);
        Assert.Equal(["pm6.s.webp", "pm6.webp", "set.json"], Directory.GetFiles(Output).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        using (var picture = Read("pm6.webp"))
        {
            Assert.Equal((230, 230), (picture.Width, picture.Height));
        }

        using var set = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Output, SpriteSetBuilder.SetFile), Ct));
        Assert.Equal("db10df75", set.RootElement.GetProperty("set").GetString());
        Assert.Equal(2, set.RootElement.GetProperty("pictures").GetInt32());
    }

    [Fact]
    public void Build_WithoutTheFolder_SaysSo() =>
        Assert.Throws<DirectoryNotFoundException>(() => SpriteSetBuilder.Build(Source, Output, "x"));

    [Fact]
    public async Task TheCommand_BuildsTheSet()
    {
        Write("pm6.icon.png", Png(64, 64, new SKRectI(1, 1, 60, 60)));

        Assert.Equal(0, await JobHost.RunAsync(["sprites", "--source", Source, "--out", Output, "--set", "abc"], cancellationToken: Ct));
        Assert.True(File.Exists(Path.Combine(Output, "pm6.webp")));
    }

    [Fact]
    public async Task TheCommand_FailsWhenThereIsNothingToBuild()
    {
        Assert.Equal(1, await JobHost.RunAsync(["sprites", "--source", Source, "--out", Output], cancellationToken: Ct));
        Write("pm9.icon.png", Png(64, 64, content: null));
        Assert.Equal(1, await JobHost.RunAsync(["sprites", "--source", Source, "--out", Output], cancellationToken: Ct));
    }

    [Theory]
    [InlineData("sprites")]
    [InlineData("sprites", "--source", "a")]
    [InlineData("sprites", "--source", "a", "--out")]
    [InlineData("sprites", "--source", "a", "--out", "b", "--colour", "red")]
    public async Task TheCommand_ShowsTheUsage_ForWrongArguments(params string[] args) =>
        Assert.Equal(JobHost.UsageExitCode, await JobHost.RunAsync(args, cancellationToken: Ct));
}
