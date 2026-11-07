# Pokémon pictures (sprites)

How the app gets its Pokémon pictures (E02-F05, spec items 1–2; decision 2026-11-07 in CLAUDE.md). Two halves:

1. **The pictures themselves** are built into the web app at every QA and prod deploy (option B, for the MVP).
2. **Which picture each form shows** is worked out by the daily import and stored on `forms`; the API passes the names to the screens.

**After the MVP, move to Blob storage (option A)**: the daily import builds the pictures and writes them to a public, read-only Azure Blob container per copy (no CDN; a few cents a month); the web app's `spriteFolder` (`web/src/ui/sprites.ts`) points there and the deploys stop building them. Nothing else changes: the names, the fallbacks and the `sprites` command stay. That removes B's one limitation (below).

## Where they come from

[PokeMiners' `pogo_assets`](https://github.com/PokeMiners/pogo_assets), folder `Images/Pokemon - 256x256/Addressable Assets`: the game's own icons, about 3,850 files named

| Picture | File |
|---|---|
| Plain form | `pm6.icon.png` |
| Shiny | `pm6.s.icon.png` |
| Form, Mega, Primal, regional | `pm6.fMEGA_X.icon.png`, `pm382.fPRIMAL.icon.png`, `pm37.fALOLA.icon.png` |
| G-Max | `pm6.fGIGANTAMAX.icon.png` |
| Event costume (not used) | `pm25.cFALL_2018.icon.png` |
| Female variant (not used) | `pm3.g2.icon.png` |

The names aren't consistent: Kyurem's plain picture is `pm646.fNORMAL`, Unown's letters are `pm201.fUNOWN_A`, and some species (Toxtricity, Shellos, Spinda) have no plain picture at all, only their forms'. Every **released** Pokémon has a picture today; the 61 Pokédex numbers without one are all unreleased in GO (Great Tusk, Terapagos and so on). They get their pictures when PokeMiners adds them, usually when they come out.

**Licensing**: the pictures are the game's copyrighted art, datamined. Fine for this personal fan project; **recheck before any public or commercial launch** (decision 2026-10-26). They are never committed to this repository (`.gitignore`: `web/public/sprites/`, `design/source/sprites/`).

## Building the pictures (web deploys)

`.github/scripts/build_sprites.sh <output folder>`:

1. Finds PokeMiners' latest commit (`git ls-remote`), or uses `POKEMINERS_COMMIT`.
2. Downloads only the folder's `pm*.icon.png` files without costumes or female variants (a sparse, history-less clone: about 150 MB, 20 seconds).
3. Runs the jobs' **`sprites` command** (`jobs/MyPoGoCompanion.Jobs/Sprites/SpriteSetBuilder.cs`, SkiaSharp, MIT): each picture is **cropped to what isn't transparent** (alpha above 8), **centred on a transparent square**, **scaled down to 256 px** when bigger (never up), and saved as **WebP** (quality 85). So every Pokémon fills its box equally, as in the design (`design/source/CONVENTIONS.md`).
4. Writes `set.json` (`{"set": "<PokeMiners commit>", "pictures": 2861, "built": "…"}`).

Today: **2,861 pictures, 24 MB** (Static Web Apps' free plan allows 250 MB per app).

In the deploys (`deploy-qa.yml`, `deploy-prod.yml`, steps under "Pokémon pictures"), before the web build:

- The output is kept in the **Actions cache** under `sprites-<PokeMiners commit>-<hash of the picture code>`, so most deploys only restore it.
- If GitHub can't say PokeMiners' latest commit, or the build fails, the **last saved set** is used (a warning). With none at all, the web app goes out **without pictures** (placeholders) and a warning; the next deploy builds them.
- The build passes the set's name as `VITE_SPRITE_SET` (the installed app's picture cache, below), and the run's Summary page says which set went out.
- A rollback to a commit from before the pictures has no script: nothing is built (that version doesn't show pictures).

On your machine (needs git and the .NET SDK):

```bash
.github/scripts/build_sprites.sh web/public/sprites
```

Without it the dev server shows placeholders everywhere.

### Serving them

- `web/public/staticwebapp.config.json`: `/sprites/*` is served as `image/webp`, **kept by browsers for a week** (`Cache-Control: public, max-age=604800`; not "forever", because a redrawn picture keeps its name), and a missing picture is a 404, never the app's page.
- The service worker (`web/src/pwa/worker.ts`) **saves each picture as it is shown** and answers from the saved copy afterwards, so a picture seen once also shows offline. The saved pictures are kept per picture set (`sprites-<set>`): a deploy with the same set keeps them, a new set starts again. They are not in the install-time list (3,000 files).

**B's limitation**: a picture PokeMiners adds after the last deploy only appears with the next prod deploy (QA: the next QA deploy). Until then that form shows its main form's picture or the placeholder.

## Which picture each form shows (daily import)

Part of the daily import (`jobs/MyPoGoCompanion.Jobs/DailyImport/Sprites.cs`), before the sanity checks. For each form:

1. **Names to try** (`SpriteNames.Candidates`):
   - a Mega or Primal by its PvPoke id: `charizard_mega_x` → `pm6.fMEGA_X`, `kyogre_primal` → `pm382.fPRIMAL`;
   - another form by its game master template: `VULPIX_ALOLA` → `pm37.fALOLA`, then `pm37.fVULPIX_ALOLA`;
   - a plain form (template without a form, or `_NORMAL`): `pm37`, then `pm37.fNORMAL`, then the pictures of the game master's other forms with the same stats and types (Toxtricity → `pm849.fAMPED`, Unown → `pm201.fUNOWN_A`);
   - a form the game master doesn't have: the plain names if it is the species' main form, else nothing.
2. **Checked in PokeMiners' folder** with a HEAD request to `raw.githubusercontent.com` (`SourceDownloader.CheckSpriteAsync`, 8 at a time, 15 s each, tried twice). The first name that exists is the form's own picture; then its shiny (`.s`) and, for a form that can Gigantamax, `pm<dex>.fGIGANTAMAX`.
3. **Fallback**: a form without its own picture shows its **main form's** (a Mega's base form, otherwise the species' main form), shiny included; with none, nothing (the app shows a placeholder).

Stored on `forms`: `sprite`, `sprite_shiny`, `sprite_gigantamax` (names like `pm37.fALOLA`; the web app adds `/sprites/` and `.webp`).

**Fewer checks on later runs**: a picture found once is trusted (the stored name is one of the form's own names), so the first import checks about 2,600 names and later imports about 200 (the forms still without their own picture). An import whose sources haven't changed doesn't run at all ("unchanged"), so a picture PokeMiners adds is noticed at the next import with new sources.

**When GitHub doesn't answer clearly** (429, 403, no answer twice): the checks stop for that run and every form not checked **keeps the pictures it had** (a warning); the next import checks them again. The import itself never fails because of the pictures.

**Run log warnings** (today's real files, all forms): released forms that show their main form's picture (none today), released forms with no picture at all (none), forms not released with no picture yet (86), released forms that can Gigantamax without a G-Max picture (1: Urshifu (Rapid Strike), whose G-Max pictures PokeMiners names `fBREAD_DOUGH_MODE`), and checks that couldn't be made.

## In the web app

- `web/src/ui/sprites.ts`: `spriteUrl('pm37.fALOLA')` → `/sprites/pm37.fALOLA.webp`; `pictureSources(form, { shiny, gigantamax })` → the pictures to try, best first (shiny or G-Max, then the normal one).
- `SpriteBox` (`web/src/ui/SpriteBox.tsx`) takes one picture or that list: when one fails to load it tries the next, then shows the placeholder of the same size, so lines never jump.
- The `/design` gallery (QA and local only) shows real pictures: Charizard, shiny, Mega X, G-Max, Alolan Vulpix, a shiny that falls back to the normal one (Urshifu (Rapid Strike)) and the placeholder.

No screen lists Pokémon yet: the Pokédex (E03-F01) is the first to send these names from the API.

## Tests

- **Unit** (`jobs/MyPoGoCompanion.Jobs.UnitTests`): `SpritesTests.cs` (names, fallbacks, trusted names, unclear answers, the HEAD checks), `SpriteSetBuilderTests.cs` (cropping, squaring, scaling, left-out files, `set.json`, the command line); `DailyImportJobTests` (the import stores the pictures and trusts them next time). Web: `sprites.test.ts`, `ui.test.tsx` (SpriteBox's fallback list), `worker.test.ts` (the picture cache), `hosting.test.ts` (headers).
- **Integration** (`tests/MyPoGoCompanion.IntegrationTests/DailyImportTests.cs`): the import's pictures in PostgreSQL.
- **UI** (`tests/ui/specs/pictures.spec.ts`): the gallery's real pictures load and are square (skipped on a copy without pictures), placeholders when none load, and on QA the hosting headers and the 404.
