# Golden files: keeper filters

What the Python keeper-filters build (`legacy/keeper-filters/`, methodology in `docs/KEEPER-FILTERS.md`) produced from one snapshot of its source data. The C# port of the calculation engine (E02-F03) reads the same inputs and must produce the same outputs; then the Python pipeline can be removed.

| Path | What it is |
|---|---|
| `inputs/` | The source data, laid out like the pipeline's `data/` folder and trimmed to what it reads: PvPoke `gamemaster.json` and `Pokemon.js` (the CP multiplier table) in full; the two PvPoke rankings files cut to `speciesId` in ranking order (only the top 150 is used); Niantic's `GAME_MASTER.json` cut from about 19 MB to the move templates and the Pokémon templates with Mega/Primal data, each kept unchanged. |
| `outputs/pvp_boxes.json` | PvP: the appraisal box per Pokédex number and league, with its F1, keeper counts, the evolutions it was judged by and their rank-1 spreads. |
| `outputs/pvp_strings.json` | PvP: the Great and Ultra League search strings, with their Pokédex ranges. |
| `outputs/raiders.json` | Raids: every scored entry per attacking type, with its best same-type moveset, DPS, TDO, score and every intermediate value, and the picks. |
| `outputs/data_pvp.json`, `data_raid.json`, `data_mega.json` | What the page shows: family rows with their own strings, the raid and Max strings with the per-type tables, the Mega lists and strings. |
| `outputs/searchsim.json` | Search simulator: about 35 expressions (every kind of term, negation, `&` `|` `,` `;` `:`, flags, and real strings from the outputs), each with every match in a small universe (13 Pokédex numbers × 8 appraisal bar sets × 5 CP values), and terms the simulator rejects. |
| `manifest.json` | The PvPoke and game master commits, the Python and numpy versions, and the size and SHA-256 of every file. |

Current snapshot: PvPoke `c1d4bfb` (2026-10-30), game master `b4a31cc` (2026-10-29).

## Comparing C# output (the harness)

`tests/MyPoGoCompanion.GoldenFiles` (reference it from the engine's tests):

```csharp
var golden = GoldenDirectory.KeeperFilters;                      // this folder, found from the repository root
var gameMaster = golden.Input("gm/GAME_MASTER.json");            // feed the frozen inputs to the C# engine
GoldenAssert.Matches(golden, "pvp_strings", actualJsonNode);     // exact; fails with a readable diff
GoldenAssert.Matches(golden, "raiders", actual, serializerOptions,
    new JsonCompareOptions { RelativeTolerance = 1e-12 });       // only where floating-point maths may differ in the last digit
```

On a mismatch the message lists up to 20 differences by path (`$.GL.strings[0].text: strings differ at character 812 …` with the text around it), and the C# output is written to `TestResults/golden-actual/<name>.json` to open next to the golden file in a diff tool. Numbers compare by value (`40` equals `40.0`); objects ignore property order; arrays compare item by item.

`MyPoGoCompanion.GoldenFiles.UnitTests` also checks this folder on every build: every file matches `manifest.json`, the strings cover Pokédex 1–1025 once and fit 1,950 characters, every box holds its rank-1 spreads, the raid lists are sorted, and the simulator cases stay inside their universe.

## Refreshing

Only when the Python pipeline changes on purpose, or to move to newer data before the C# port is compared:

```bash
cd legacy/keeper-filters
python build.py                   # fetch the latest data into data/ and build
python scripts/golden.py freeze   # builds from data/ and from the trimmed inputs, stops if they differ, then writes this folder
python scripts/golden.py check    # what CI runs: rebuild from inputs/ (no network) and compare
```

Commit the whole folder together. The `build-and-unit` check runs `check` on every pull request, so a change to the Python pipeline that alters its output fails until the golden files are refreshed.

## Licences

PvPoke data is MIT. The game master is datamined from Pokémon GO; its repository says the content belongs to The Pokémon Company and Niantic and is shared for educational use. The excerpt here is test data for a free, unofficial fan project; recheck before any commercial launch (as for the rest of the game data).
