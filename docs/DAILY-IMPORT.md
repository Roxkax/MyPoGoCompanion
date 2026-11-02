# Daily import

The job that keeps the game data up to date (E02-F01, spec 16.1): every morning it downloads PvPoke's files and the datamined game master, and brings the database's species, forms, moves, movepools, evolutions, CP multipliers and PvP rankings in line with them. The code is `jobs/MyPoGoCompanion.Jobs/DailyImport/`; the decisions behind it are in CLAUDE.md's decision log (2026-11-02).

Later features build on it: Portuguese names from the game text (E02-F02), the recalculated rankings, IVs and search filters (E02-F03), the sanity checks and the admin's "Run now" button (E02-F04), and the admin's Data status screen (E10-F01).

## Where it runs

| | Prod | QA |
|---|---|---|
| Container Apps Job | `job-import-prod` | `job-import-qa` |
| When | **Every day at 06:00 UTC** (Azure's schedules are UTC only: 06:00 in Lisbon in winter, 07:00 in summer); once more right after a prod deploy that changed the import's code or the schema | After **every QA deploy**, and by hand. No schedule: QA's database also gets prod's data at each prod deploy (the QA reset) |
| By hand | **Actions → Run daily import → Run workflow**, `prod` | the same, `qa` |

Both jobs run the image `ghcr.io/roxkax/mypogocompanion-jobs:<commit>` (`jobs/MyPoGoCompanion.Jobs/Dockerfile`), built and deployed by the QA and prod deploys (`.github/scripts/deploy_job.sh`): 0.5 vCPU and 1 GiB, one run at a time, 30 minutes at most, no automatic retry (the job retries its downloads itself; a failed scheduled run waits for the next day). A run takes about a minute, well inside Azure's free monthly allowance for Container Apps, so it costs nothing.

The deploys and the **Run daily import** workflow start a run with `.github/scripts/run_import.sh`, wait for it, and show its run log on the GitHub run's Summary page (status, duration, changes per kind, warnings). A failed run fails that step: on QA the deploy check goes red, on prod the "Prod deploy failed" issue opens. A failed **scheduled** run raises no alert (decision 2026-11-02): it shows in the run log, and after 3 days without a successful run the out-of-date warning appears on the game data screens.

## What it reads

| File | From | Used for |
|---|---|---|
| `src/data/gamemaster.json` | PvPoke (MIT) | **Main source**: species, form ids, base stats, types, movepools with Elite TM and Legacy moves, released, Shadow released, families, nicknames, buddy distance, second-move cost, Legendary/Mythical/Ultra Beast, PvP move numbers |
| `src/data/rankings/all/overall/rankings-1500.json`, `-2500.json`, `-10000.json` | PvPoke (MIT) | Great, Ultra and Master League rank, score and recommended moveset |
| `src/js/pokemon/Pokemon.js` | PvPoke (MIT) | The CP multiplier per level (the table the Python keeper build used), levels 1–51 |
| `GAME_MASTER.json` (19 MB) | `alexelgt/game_masters` (datamined) | Raid (PvE) move numbers, evolution steps (candy, trade discount, item and how many, buddy km, day/night, lure, gender, special task), Dynamax and Gigantamax |

All are fetched over HTTPS from `raw.githubusercontent.com` (addresses in `DailyImportOptions`, overridable with `DailyImport__PvpokeBaseUrl` / `DailyImport__GameMasterUrl`). Each download is tried 3 times (5 s, then 10 s apart) when the answer may be temporary (no answer, timeout, 408, 429, 5xx); a 404 fails at once.

**Not in the sources**: which shinies are released (stays unknown until the weekly AI researcher fills it, E05), and the Portuguese names (E02-F02; types already have the game's Portuguese names). The game master's Dynamax and Gigantamax looks are what the game's code can do, so a few may show before players can get them (decision 2026-11-02).

**Licensing**: PvPoke is MIT (credited in Settings → About). The game master is datamined from Niantic's game: fine for this personal fan project, **recheck before any commercial launch** (as for the golden-file inputs).

## How the sources become rows

- **Forms** are PvPoke's ids (`charizard`, `charizard_mega_x`, `vulpix_alolan`). PvPoke's `_shadow` entries are not rows: they set `shadow_released` on their form. Kinds: Mega/Primal (PvPoke's `mega` tag; their `base_form_id` is the species' main form), Regional (Alolan, Galarian, Hisuian, Paldean), Base (the main form), Alternate (everything else).
- **The main form** (the Pokédex line, one per species) is the form with the stats and types of the game master's template without a form name, else the one with its stats, else PvPoke's first. This picks Zacian's Hero and Darmanitan's Standard although PvPoke lists Crowned Sword and Galarian Zen first. When it moves to another form, the old main form is cleared first (one main form per species is a unique index).
- **Matching PvPoke to the game master**: by name (`vulpix_alolan` → `VULPIX_ALOLA`, or `…_NORMAL`), else the template of the same Pokédex number with the same stats and types. 1,204 of 1,206 forms match today; the two Basculegion forms aren't in the game master yet and get no evolutions or Dynamax (a warning). The match is kept in `forms.game_master_id`.
- **Moves**: fast when PvPoke gives them energy (or no cost); PvP numbers from PvPoke, raid numbers from the game master's `V0xxx_MOVE_*` templates (fast ones end in `_FAST`; `FUTURE_SIGHT`, `PYRO_BALL` and `TECHNO_BLAST_DOUSE` are spelled differently there, as in the Python build). A move with no raid version (Hidden Power, Aegislash's charge moves, the Mega `…_PLUS` moves) gets **raid numbers 0** (power 0, duration 0): the raid engine leaves those out.
- **Evolutions** come from the game master's evolution branches of each matched form; Mega Evolutions are forms, not steps. When a step exists twice (the usual one and a Community Day or event version), the usual one is kept. `task_en` describes in English what the columns don't: the special task ("Win 10 raids", "Walk 10 km with it as your buddy", "Earn 70 hearts…"), how many of an item it uses ("Uses 999 Gimmighoul Coins"), a move it must know, full moon, dusk, phone upside down.
- **Rankings**: one row per league, form and Shadow, in PvPoke's order (rank = position). Rows for a form or move the import doesn't have are left out (a warning); PvPoke's `"none"` padding in a moveset is dropped (Unown).

## Saving

The run brings every table in line with the sources and counts each kind's rows **added, changed and removed** (a row is "changed" only when a value differs, so a run on the same files changes nothing):

| Kind | Removed when the sources no longer list it |
|---|---|
| types, families, species, moves | Never removed (live data, collections and rankings point at them) |
| forms | **Kept, marked not released** (counted as removed) |
| movepools, evolutions | Deleted, for the forms the sources still list |
| CP multipliers, PvP rankings | Deleted |

What other jobs fill in is kept when the sources say nothing: shiny (the AI researcher) and the Portuguese texts (game text).

Everything is saved in **one transaction**: a run that fails anywhere changes nothing, and yesterday's data stays live.

A guard refuses sources that list **fewer than 80% of the released forms** already stored (`DailyImport__MinimumReleasedFormsShare`): a cut-off file would otherwise mark most Pokémon as not released. E02-F04 adds the fuller sanity checks.

## The run log

Each run is a row in `job_runs` (`job = 'DailyImport'`): status (Running → Succeeded or Failed), start and finish (so the duration), and:

- `summary` (JSON): `trigger` (`schedule`, `deploy` or `manual`), `version` (the job's commit), `unchanged`, `sources` (each file's SHA-256 and size), `changes` per kind (`{"forms": {"added": 3, "changed": 1, "removed": 0}, …}`) and `warnings` (what the import couldn't use, in plain words; the run still succeeds).
- `error`: why it failed, in plain words, e.g. "Couldn't download the Game master (…): the server answered 404 Not Found." or "The PvPoke game master isn't the JSON the import expects (…)". No personal data or secrets (scrubbed like the logs).

The out-of-date warning (`GET /freshness`) reads the last **successful** run.

Other rules:

- **Nothing changed**: when every source file has the same fingerprint as at the last successful run **and** the job is the same version, the run stops after the downloads as a success with `"unchanged": true` (a few seconds). A new version of the job always compares everything again.
- **One run at a time**: a PostgreSQL advisory lock (`PostgresRunLock`), held by the run's own database connection. A second run that finds it taken does nothing and leaves no run log. This needs a direct connection: the deploys give the job Neon's direct host (`npgsql_connection.py --direct`), not the pooler.
- **A run that was killed** (timeout, container restart) stays "Running" until the next run, which holds the lock and so knows it is dead: it is marked failed with "Stopped before it finished…".

## Running it locally

With the local database running (`docker compose up -d --wait`) and migrated (start the API once, or `dotnet ef database update --project api/MyPoGoCompanion.Data`):

```bash
ConnectionStrings__Database="Host=localhost;Database=mypogocompanion;Username=mypogocompanion;Password=localdev" dotnet run --project jobs/MyPoGoCompanion.Jobs -- daily-import
```

It downloads the real files (about 23 MB) and replaces the dev seed's game data with the real one (the seed's made-up values change; its forms that the sources don't list are marked not released). Settings can be passed after the command, e.g. `--DailyImport:Trigger=manual`. Exit code 0 = succeeded or nothing to do, 1 = failed, 64 = unknown command.

## Tests

- **Unit tests** (`jobs/MyPoGoCompanion.Jobs.UnitTests`): reading each file, building the game data, the English task texts, the downloader's retries, a whole run on an in-memory database (run log, nothing-changed shortcut, failures, guard, lock, kept fields, counts). They read **fixtures**: the real files cut to 28 species that cover the tricky cases (`Fixtures/make_fixtures.py`; regenerate with the folder of downloaded files when the sources change shape).
- **Integration tests** (`tests/MyPoGoCompanion.IntegrationTests/DailyImportTests.cs`): the job's command line on a real PostgreSQL 17 with the same fixtures: a first import and `/freshness`, a second one that changes nothing, a failed download that leaves the data, a main form that moves, the lock, and an import on top of the dev seed (live data and collections pointing at forms).
