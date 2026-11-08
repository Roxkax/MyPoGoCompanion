using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyPoGoCompanion.Data.GameData;
using MyPoGoCompanion.Jobs.DailyImport;

namespace MyPoGoCompanion.Jobs.UnitTests;

/// <summary>The names PokeMiners gives a form's picture (docs/SPRITES.md).</summary>
public sealed class SpriteNamesTests
{
    private static GameMasterPokemon Template(string pokemonId, string? form, int dex = 37) =>
        new(form ?? pokemonId, dex, pokemonId, form, 100, 100, 100, ["fire"], null, []);

    [Theory]
    [InlineData("charizard_mega_x", 6, "pm6.fMEGA_X")]
    [InlineData("charizard_mega_y", 6, "pm6.fMEGA_Y")]
    [InlineData("venusaur_mega", 3, "pm3.fMEGA")]
    [InlineData("kyogre_primal", 382, "pm382.fPRIMAL")]
    public void AMegaOrPrimal_IsNamedByItsPvpokeId(string formId, int dex, string expected) =>
        Assert.Equal([expected], SpriteNames.Candidates(formId, dex, isMega: true, isDefault: false, template: null));

    [Fact]
    public void AMegaWithAnUnexpectedId_HasNoName_AndShowsItsBaseForm() =>
        Assert.Empty(SpriteNames.Candidates("mewtwo_megaish", 150, isMega: true, isDefault: false, template: null));

    [Fact]
    public void AForm_TriesTheShortName_ThenTheGameMastersWholeName()
    {
        Assert.Equal(["pm37.fALOLA", "pm37.fVULPIX_ALOLA"],
            SpriteNames.Candidates("vulpix_alolan", 37, false, false, Template("VULPIX", "VULPIX_ALOLA")));
        // A form name that doesn't start with the Pokémon's (a datamine quirk) is used whole.
        Assert.Equal(["pm150.fA", "pm150.fA"], SpriteNames.Candidates("mewtwo_armored", 150, false, false, Template("MEWTWO", "A", 150)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("VULPIX_NORMAL")]
    public void APlainForm_TriesThePlainName_ThenNormal(string? form) =>
        Assert.Equal(["pm37", "pm37.fNORMAL"], SpriteNames.Candidates("vulpix", 37, false, true, Template("VULPIX", form)));

    [Fact]
    public void APlainForm_ThenTriesItsLookalikes_WithoutRepeats()
    {
        var toxtricity = Template("TOXTRICITY", null, 849);
        var lookalikes = new[] { Template("TOXTRICITY", "TOXTRICITY_AMPED", 849), Template("TOXTRICITY", "TOXTRICITY_NORMAL", 849), Template("TOXTRICITY", "TOXTRICITY_AMPED", 849) };

        Assert.Equal(["pm849", "pm849.fNORMAL", "pm849.fAMPED", "pm849.fTOXTRICITY_AMPED"],
            SpriteNames.Candidates("toxtricity", 849, false, true, toxtricity, lookalikes));
    }

    [Fact]
    public void AFormTheGameMasterLacks_IsTriedPlainOnlyWhenItIsTheMainForm()
    {
        Assert.Equal(["pm1001", "pm1001.fNORMAL"], SpriteNames.Candidates("wo_chien", 1001, false, true, null));
        Assert.Empty(SpriteNames.Candidates("pikachu_flying", 25, false, false, null));
    }

    [Fact]
    public void ShinyAndGigantamax_HaveTheirOwnNames()
    {
        Assert.Equal("pm37.fALOLA.s", SpriteNames.Shiny("pm37.fALOLA"));
        Assert.Equal("pm849.fGIGANTAMAX", SpriteNames.Gigantamax(849));
    }
}

/// <summary>Choosing every form's pictures: its own, its main form's, or none (docs/SPRITES.md).</summary>
public sealed class SpriteResolverTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Form Form(string id, int dex, bool isDefault = false, string? baseFormId = null, bool released = true, bool canGigantamax = false) => new()
    {
        Id = id,
        Dex = dex,
        IsDefault = isDefault,
        BaseFormId = baseFormId,
        Released = released,
        CanGigantamax = canGigantamax,
        NameEn = id,
        Type1Id = "fire",
    };

    /// <summary>Charizard (G-Max), its Mega X, Vulpix and Alolan Vulpix, and a Pokémon PokeMiners has nothing for.</summary>
    private static ImportedGameData Data()
    {
        var data = new ImportedGameData();
        data.Forms.AddRange([
            Form("charizard", 6, isDefault: true, canGigantamax: true),
            Form("charizard_mega_x", 6, baseFormId: "charizard"),
            Form("vulpix", 37, isDefault: true),
            Form("vulpix_alolan", 37),
            Form("greattusk", 984, isDefault: true, released: false),
        ]);
        data.SpriteCandidates["charizard"] = ["pm6", "pm6.fNORMAL"];
        data.SpriteCandidates["charizard_mega_x"] = ["pm6.fMEGA_X"];
        data.SpriteCandidates["vulpix"] = ["pm37", "pm37.fNORMAL"];
        data.SpriteCandidates["vulpix_alolan"] = ["pm37.fALOLA", "pm37.fVULPIX_ALOLA"];
        data.SpriteCandidates["greattusk"] = ["pm984", "pm984.fNORMAL"];
        return data;
    }

    private sealed class Folder(params string[] pictures)
    {
        private readonly HashSet<string> _pictures = [.. pictures];

        public List<string> Asked { get; } = [];

        public HashSet<string> Unknown { get; } = [];

        public Task<SpriteCheck> Check(string name, CancellationToken cancellationToken)
        {
            lock (Asked)
            {
                Asked.Add(name);
            }

            return Task.FromResult(Unknown.Contains(name) ? SpriteCheck.Unknown : _pictures.Contains(name) ? SpriteCheck.Exists : SpriteCheck.Missing);
        }
    }

    private static Dictionary<string, StoredSprites> Nothing => [];

    private static (string? Sprite, string? Shiny, string? Gigantamax) Pictures(ImportedGameData data, string id)
    {
        var form = data.Forms.Single(f => f.Id == id);
        return (form.Sprite, form.SpriteShiny, form.SpriteGigantamax);
    }

    [Fact]
    public async Task EachFormGetsItsOwnPictures_WhenPokeMinersHasThem()
    {
        var data = Data();
        var folder = new Folder("pm6", "pm6.s", "pm6.fGIGANTAMAX", "pm6.fMEGA_X", "pm6.fMEGA_X.s", "pm37", "pm37.s", "pm37.fVULPIX_ALOLA", "pm37.fVULPIX_ALOLA.s");

        await SpriteResolver.ResolveAsync(data, Nothing, folder.Check, 4, Ct);

        Assert.Equal(("pm6", "pm6.s", "pm6.fGIGANTAMAX"), Pictures(data, "charizard"));
        Assert.Equal(("pm6.fMEGA_X", "pm6.fMEGA_X.s", null), Pictures(data, "charizard_mega_x"));
        Assert.Equal(("pm37.fVULPIX_ALOLA", "pm37.fVULPIX_ALOLA.s", null), Pictures(data, "vulpix_alolan"));
        Assert.Contains("pm37.fALOLA", folder.Asked);
        Assert.Single(data.Warnings);
        Assert.StartsWith("1 forms not released in the game have no picture yet: greattusk", data.Warnings[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFormWithoutItsOwnPicture_ShowsItsMainForms_AndTheRunLogSaysSo()
    {
        var data = Data();
        var folder = new Folder("pm6", "pm6.s", "pm37");

        await SpriteResolver.ResolveAsync(data, Nothing, folder.Check, 1, Ct);

        // A Mega shows its base form's; a regional form its species' main form's (which has no shiny picture).
        Assert.Equal(("pm6", "pm6.s", null), Pictures(data, "charizard_mega_x"));
        Assert.Equal(("pm37", null, null), Pictures(data, "vulpix_alolan"));
        Assert.Equal(("pm6", "pm6.s", null), Pictures(data, "charizard"));
        Assert.Contains("2 released forms show their main form's picture (PokeMiners has none of their own): charizard_mega_x, vulpix_alolan", data.Warnings);
        Assert.Contains("1 released forms that can Gigantamax have no G-Max picture (the app shows the normal one): charizard", data.Warnings);
    }

    [Fact]
    public async Task AReleasedFormWithNoPictureAtAll_IsAWarning()
    {
        var data = Data();

        await SpriteResolver.ResolveAsync(data, Nothing, new Folder().Check, 4, Ct);

        Assert.All(data.Forms, form => Assert.Null(form.Sprite));
        Assert.Contains(data.Warnings, w => w.StartsWith("4 released forms have no picture (the app shows a placeholder): charizard, charizard_mega_x, vulpix, vulpix_alolan", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PicturesFoundBefore_AreTrusted_WithoutAskingAgain()
    {
        var data = Data();
        var stored = new Dictionary<string, StoredSprites>
        {
            ["charizard"] = new("pm6", "pm6.s", "pm6.fGIGANTAMAX"),
            // A main form's picture (a fallback) isn't one of the Mega's own names: it is checked again.
            ["charizard_mega_x"] = new("pm6", "pm6.s", null),
        };
        var folder = new Folder("pm6.fMEGA_X");

        await SpriteResolver.ResolveAsync(data, stored, folder.Check, 4, Ct);

        Assert.DoesNotContain("pm6", folder.Asked);
        Assert.DoesNotContain("pm6.s", folder.Asked);
        Assert.DoesNotContain("pm6.fGIGANTAMAX", folder.Asked);
        Assert.Contains("pm6.fMEGA_X", folder.Asked);
        Assert.Equal(("pm6", "pm6.s", "pm6.fGIGANTAMAX"), Pictures(data, "charizard"));
        Assert.Equal(("pm6.fMEGA_X", null, null), Pictures(data, "charizard_mega_x"));
    }

    [Fact]
    public async Task WhenGitHubDoesntAnswerClearly_AFormKeepsThePicturesItHad()
    {
        var data = Data();
        var stored = new Dictionary<string, StoredSprites>
        {
            ["vulpix"] = new("pm37", "pm37.s", null),
            ["vulpix_alolan"] = new("pm37", "pm37.s", null),
        };
        var folder = new Folder("pm6", "pm6.s", "pm6.fGIGANTAMAX");
        folder.Unknown.UnionWith(["pm37", "pm37.fALOLA", "pm6.fMEGA_X", "pm984"]);

        await SpriteResolver.ResolveAsync(data, stored, folder.Check, 4, Ct);

        Assert.Equal(("pm37", "pm37.s", null), Pictures(data, "vulpix"));
        Assert.Equal(("pm37", "pm37.s", null), Pictures(data, "vulpix_alolan"));
        Assert.Equal((null, null, null), Pictures(data, "charizard_mega_x"));
        Assert.Equal(("pm6", "pm6.s", "pm6.fGIGANTAMAX"), Pictures(data, "charizard"));
        // Vulpix's stored picture is one of its own names, so it is trusted without a check: three forms weren't checked.
        Assert.Equal("Couldn't check every picture (GitHub didn't answer clearly): 3 forms keep the pictures they had; the next import checks them again.", Assert.Single(data.Warnings));
    }

    [Fact]
    public async Task AFormWhoseMainFormWasntChecked_UsesTheMainFormsStoredPictures()
    {
        var data = Data();
        var stored = new Dictionary<string, StoredSprites> { ["vulpix"] = new("pm37", "pm37.s", null) };
        var folder = new Folder();
        folder.Unknown.Add("pm37");

        await SpriteResolver.ResolveAsync(data, stored, folder.Check, 1, Ct);

        Assert.Equal(("pm37", "pm37.s", null), Pictures(data, "vulpix_alolan"));
    }

    [Theory]
    [InlineData("pm6.s")]
    [InlineData("pm6.fGIGANTAMAX")]
    public async Task AnUnclearShinyOrGigantamaxCheck_KeepsTheFormsPictures(string unclear)
    {
        var data = Data();
        var stored = new Dictionary<string, StoredSprites> { ["charizard"] = new("pm6", null, null) };
        var folder = new Folder("pm6", "pm6.s", "pm6.fGIGANTAMAX");
        folder.Unknown.Add(unclear);

        await SpriteResolver.ResolveAsync(data, stored, folder.Check, 1, Ct);

        Assert.Equal(("pm6", null, null), Pictures(data, "charizard"));
    }
}

/// <summary>Asking GitHub whether PokeMiners' folder has a picture.</summary>
public sealed class SpriteCheckTests
{
    private static readonly DailyImportOptions _options = new() { SpriteCheckTimeout = TimeSpan.FromSeconds(5) };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SourceDownloader Downloader(HttpMessageHandler handler, DailyImportOptions? options = null) =>
        new(new HttpClient(handler), Options.Create(options ?? _options), NullLogger<SourceDownloader>.Instance);

    private static string Url(string sprite) => _options.SpriteBaseUrl.AbsoluteUri + sprite + ".icon.png";

    [Fact]
    public async Task ThePictureIsThere_OrNot()
    {
        var http = new Answers(name => name.Contains("pm6.icon", StringComparison.Ordinal) ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        var downloader = Downloader(http);

        Assert.Equal(SpriteCheck.Exists, await downloader.CheckSpriteAsync("pm6", Ct));
        Assert.Equal(SpriteCheck.Missing, await downloader.CheckSpriteAsync("pm6.fMEGA_Z", Ct));
        Assert.Equal([(HttpMethod.Head, Url("pm6")), (HttpMethod.Head, Url("pm6.fMEGA_Z"))], http.Requests);
        Assert.Equal(
            "https://raw.githubusercontent.com/PokeMiners/pogo_assets/master/Images/Pokemon%20-%20256x256/Addressable%20Assets/pm6.icon.png",
            http.Requests[0].Url);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task WhenGitHubRefuses_EveryLaterCheckIsUnknown_WithoutAskingAgain(HttpStatusCode refusal)
    {
        var http = new Answers(_ => refusal);
        var downloader = Downloader(http);

        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm6", Ct));
        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm7", Ct));
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task AServerError_IsTriedTwice_ThenUnknown_ButLaterChecksGoOn()
    {
        var calls = 0;
        var http = new Answers(_ => ++calls <= 2 ? HttpStatusCode.BadGateway : HttpStatusCode.OK);
        var downloader = Downloader(http);

        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm6", Ct));
        Assert.Equal(SpriteCheck.Exists, await downloader.CheckSpriteAsync("pm7", Ct));
    }

    [Fact]
    public async Task AServerErrorOnce_IsTriedAgain()
    {
        var calls = 0;
        var downloader = Downloader(new Answers(_ => ++calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK));

        Assert.Equal(SpriteCheck.Exists, await downloader.CheckSpriteAsync("pm6", Ct));
    }

    [Fact]
    public async Task NoAnswerTwice_StopsTheChecks()
    {
        var http = new Answers(_ => throw new HttpRequestException(HttpRequestError.ConnectionError, "down"));
        var downloader = Downloader(http);

        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm6", Ct));
        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm7", Ct));
        Assert.Equal(2, http.Requests.Count);
    }

    [Fact]
    public async Task NoAnswerInTime_StopsTheChecks()
    {
        var http = new Answers(_ => throw new TaskCanceledException("slow"));
        var downloader = Downloader(http);

        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm6", Ct));
        Assert.Equal(SpriteCheck.Unknown, await downloader.CheckSpriteAsync("pm7", Ct));
        Assert.Equal(2, http.Requests.Count);
    }

    private sealed class Answers(Func<string, HttpStatusCode> answer) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add((request.Method, url));
            return Task.FromResult(new HttpResponseMessage(answer(url)));
        }
    }
}
