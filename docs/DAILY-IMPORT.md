# Daily import

The job that keeps the game data up to date (E02-F01, spec 16.1): every morning it downloads PvPoke's files, the datamined game master and the game's Portuguese text, and brings the database's species, forms, moves, movepools, evolutions, CP multipliers, PvP rankings and Portuguese names (E02-F02, spec 16.2) in line with them. From the same files the calculation engine then works out the PvP best IVs, keeper boxes and raid rankings (E02-F03, `docs/ENGINE.md`). The code is `jobs/MyPoGoCompanion.Jobs/DailyImport/`; the decisions behind it are in [the project decision log](PROJECT-CONTEXT.md#decision-log) (2026-11-02).

It also works out which **picture** each form shows (E02-F05, [`docs/SPRITES.md`](SPRITES.md)): it checks which of the form's possible picture names PokeMiners has and stores them on `forms`.

Before publishing, every run checks the new data and holds anything that would go missing for the super admin (E02-F04, [below](#checking-and-publishing)); the admin can start a run from the API (**Run now**). Later features build on it: the search filters (E04-F01) and the admin's Data status screen (E10-F01).

## Where it runs

| | Prod | QA |
|---|---|---|
| Container Apps Job | `job-import-prod` | `job-import-qa` |
| When | **Every day at 06:00 UTC**, started by the **Run daily import** workflow's schedule (06:00 in Lisbon in winter, 07:00 in summer; GitHub may start it 5–30 minutes late); once more right after a prod deploy that changed the import's code, the engine or the schema | After **every QA deploy**, and by hand. No schedule: QA's database also gets prod's data at each prod deploy (the QA reset) |
| By hand | **Actions → Run daily import → Run workflow**, `prod` (tick **Publish what's missing** to publish a held run's list), or **Run now** (`POST /admin/jobs/daily-import/run`, admins) | the same, `qa` |

Both jobs run the image `ghcr.io/roxkax/mypogocompanion-jobs:<commit>` (`jobs/MyPoGoCompanion.Jobs/Dockerfile`), built and deployed by the QA and prod deploys (`.github/scripts/deploy_job.sh`): 0.5 vCPU and 1 GiB, one run at a time, 30 minutes at most, no automatic retry (the job retries its downloads itself; a failed scheduled run waits for the next day). Both are **started by hand** in Azure's terms: the schedule is GitHub's (decision 2026-11-02), so a held or failed run can open the owner's issue; the deploy switched `job-import-prod` off Azure's own schedule. A run takes about a minute (the calculations add about 2 s, saving their rows on the first run some more), well inside Azure's free monthly allowance for Container Apps, so it costs nothing.

The deploys and the **Run daily import** workflow start a run with `.github/scripts/run_import.sh`, wait for it, and show its run log on the GitHub run's Summary page (status, duration, changes per kind, what's missing, the sanity checks, warnings). A failed run fails that step (on QA the deploy check goes red); a held run doesn't (it's a warning). **On prod**, a held run opens (or comments on) the issue **"Daily import needs your decision"** and a failed one **"Daily import failed"** (label `import-alert`, assigned to the owner, `.github/scripts/import_alert.py`); the next published run closes it. A failed prod deploy still opens "Prod deploy failed" as well. Runs started with Run now open no issue (the admin is watching).

## What it reads

| File | From | Used for |
|---|---|---|
| `src/data/gamemaster.json` | PvPoke (MIT) | **Main source**: species, form ids, base stats, types, movepools with Elite TM and Legacy moves, released, Shadow released, families, nicknames, buddy distance, second-move cost, Legendary/Mythical/Ultra Beast, PvP move numbers |
| `src/data/rankings/all/overall/rankings-1500.json`, `-2500.json`, `-10000.json` | PvPoke (MIT) | Great, Ultra and Master League rank, score and recommended moveset |
| `src/js/pokemon/Pokemon.js` | PvPoke (MIT) | The CP multiplier per level (the table the Python keeper build used), levels 1–51 |
| `GAME_MASTER.json` (19 MB) | `alexelgt/game_masters` (datamined) | Raid (PvE) move numbers, evolution steps (candy, trade discount, item and how many, buddy km, day/night, lure, gender, special task), Dynamax and Gigantamax; each move's number (for its name) |
| `Release/Brazilian Portuguese/pt-br_raw.json` (4 MB) | `sora10pls/holoholo-text` (datamined) | The game's Portuguese text from the latest release: Pokémon, Mega, form, move and type names. **Optional**, see below |
| `Remote/Brazilian Portuguese/pt-br_raw.json` (70 KB) | `sora10pls/holoholo-text` (datamined) | The newer texts the game's server sends on top of the release (they win) |

The pictures aren't downloaded: the import only asks whether PokeMiners' picture folder has a name (a HEAD request per name, `DailyImport__SpriteBaseUrl`; about 2,600 on a first import, about 200 later), see `docs/SPRITES.md`. When GitHub refuses or doesn't answer, the forms keep the pictures they had (a warning); the import never fails because of them.

All are fetched over HTTPS from `raw.githubusercontent.com` (addresses in `DailyImportOptions`, overridable with `DailyImport__PvpokeBaseUrl` / `DailyImport__GameMasterUrl` / `DailyImport__GameTextUrl` / `DailyImport__GameTextRemoteUrl`). Each download is tried 3 times (5 s, then 10 s apart) when the answer may be temporary (no answer, timeout, 408, 429, 5xx); a 404 fails at once.

**Not in the sources**: which shinies are released (stays unknown until the weekly AI researcher fills it, E05). The game master's Dynamax and Gigantamax looks are what the game's code can do, so a few may show before players can get them (decision 2026-11-02).

**Licensing**: PvPoke is MIT (credited in Settings → About). The game master and the game text are datamined from the game: fine for this personal fan project, **recheck before any commercial launch** (as for the golden-file inputs). PokeMiners' `pogo_assets` text was not used: it stops at Pokédex #1008 and move #487.

## How the sources become rows

- **Forms** are PvPoke's ids (`charizard`, `charizard_mega_x`, `vulpix_alolan`). PvPoke's `_shadow` entries are not rows: they set `shadow_released` on their form. Kinds: Mega/Primal (PvPoke's `mega` tag; their `base_form_id` is the species' main form), Regional (Alolan, Galarian, Hisuian, Paldean), Base (the main form), Alternate (everything else).
- **The main form** (the Pokédex line, one per species) is the form with the stats and types of the game master's template without a form name, else the one with its stats, else PvPoke's first. This picks Zacian's Hero and Darmanitan's Standard although PvPoke lists Crowned Sword and Galarian Zen first. When it moves to another form, the old main form is cleared first (one main form per species is a unique index).
- **Matching PvPoke to the game master**: by name (`vulpix_alolan` → `VULPIX_ALOLA`, or `…_NORMAL`), else the template of the same Pokédex number with the same stats and types. 1,204 of 1,206 forms match today; the two Basculegion forms aren't in the game master yet and get no evolutions or Dynamax (a warning). The match is kept in `forms.game_master_id`.
- **Moves**: fast when PvPoke gives them energy (or no cost); PvP numbers from PvPoke, raid numbers from the game master's `V0xxx_MOVE_*` templates (fast ones end in `_FAST`; `FUTURE_SIGHT`, `PYRO_BALL` and `TECHNO_BLAST_DOUSE` are spelled differently there, as in the Python build). A move with no raid version (Hidden Power, Aegislash's charge moves, the Mega `…_PLUS` moves) gets **raid numbers 0** (power 0, duration 0): the raid engine leaves those out.
- **Evolutions** come from the game master's evolution branches of each matched form; Mega Evolutions are forms, not steps. When a step exists twice (the usual one and a Community Day or event version), the usual one is kept. `task_en` describes in English what the columns don't: the special task ("Win 10 raids", "Walk 10 km with it as your buddy", "Earn 70 hearts…"), how many of an item it uses ("Uses 999 Gimmighoul Coins"), a move it must know, full moon, dusk, phone upside down.
- **Rankings**: one row per league, form and Shadow, in PvPoke's order (rank = position). Rows for a form or move the import doesn't have are left out (a warning); PvPoke's `"none"` padding in a moveset is dropped (Unown).
- **The engine's results** (`EngineResults`, `docs/ENGINE.md`): rank-1 PvP IVs per form (Great, Ultra, Master), the keeper box per Pokédex number (Great, Ultra), the raid ranking per attacking type and every form's raid movesets, calculated from PvPoke's entries (Shadows included) and the game master's raid move numbers, as the Python build did. A CP multiplier table that misses levels (never PvPoke's) skips them with a warning; a Pokédex number with no form to judge its box fails the run.

## Portuguese names

The game's Portuguese is Brazilian Portuguese; the app uses it for Pokémon, move and type names (decision 2026-11-01). `PortugueseNames` names each row:

| What | From the game text | Example |
|---|---|---|
| Species | `pokemon_name_0006` | Charizard, *Tipo Nulo*, *Presa Grande* |
| Types | `pokemon_type_fire` (the built-in list when there is no text) | *Fogo* |
| Megas and Primals | `pokemon_name_0006_0002` (0001 Mega, 0002 Mega X, 0003 Mega Y, 0004 Primal) | *Mega Charizard X*, *Kyogre Primitivo* |
| Regional forms | species + region | *Vulpix de Alola*, *Darmanitan de Galar (Modo Zen)* |
| Other forms | species + the form text in brackets, found by the game master's form name (`form_giratina_origin`, else `form_origin`), else by PvPoke's bracket word (`form_female`) | *Giratina (Forma Origem)*, *Basculegion (Fêmea)* |
| | a form text that already names the Pokémon is used as it is | *Kyurem Branco*, *Rotom Ventilador* |
| | a species with one form uses the species name | *Mime Jr.* |
| Moves | `move_name_0269`, by the game master's move number (`V0269_MOVE_FIRE_SPIN_FAST`) | *Chama Furacão* |
| | PvPoke's own moves from the move they stand for | *Surfar+*, *Poder Oculto (Inseto)*, Aegislash's *Golpe de Ar* |

A text can point at another one (`<<move_name_0296>>+`); the import resolves it. On today's files every species and type is named, 1,251 of 1,269 forms and 352 of 355 moves. **A name the text doesn't have stays null and the app shows the English one**; the run log lists them as warnings ("15 forms without a Portuguese name in the game text …": costume Pikachus, Wormadam, Zygarde 50%, Calyrex Ice Rider…). Evolution tasks (`task_pt`) aren't game names and stay English for now.

**The game text is optional** (decision 2026-11-02, option A): when it can't be downloaded (after the same 3 tries) or isn't the expected JSON, the import still updates everything else, **keeps the Portuguese names it has** and says so in a warning ("Portuguese names not updated, the last ones are kept: …"). Without the server's newer texts it uses the release's ("Portuguese names from the game release only …"). A missing text file also leaves its fingerprint out of the run log, so the next run with it is never taken as "nothing changed".

## Saving

The run brings every table in line with the sources and counts each kind's rows **added, changed and removed** (a row is "changed" only when a value differs, so a run on the same files changes nothing):

| Kind | Removed when the sources no longer list it |
|---|---|
| types, families, species, moves | Never removed (live data, collections and rankings point at them) |
| forms | **Kept, marked not released** (counted as removed) |
| movepools, evolutions | Deleted, for the forms the sources still list |
| CP multipliers, PvP rankings, PvP best IVs, keeper boxes, raid rankings, raid movesets | Deleted |

A form's pictures (`sprite`, `sprite_shiny`, `sprite_gigantamax`) are counted with the forms' changes; a form GitHub couldn't be asked about keeps its own.

What the sources don't say this time is kept: shiny (the AI researcher fills it), the evolution tasks' Portuguese text, and a Portuguese name the game text no longer has or couldn't be downloaded.

Everything is saved in **one transaction**: a run that fails anywhere changes nothing, and yesterday's data stays live.

## Checking and publishing

Decisions 2026-11-02 (E02-F04). Between building the new data and committing it, every run:

1. **Runs the sanity checks** (`ImportChecks`) on the new data. Broken data can't be published: a failed check **fails the run**, nothing changes and the run log says what is wrong (the first 10 problems per check, the rest counted):

   | Check | What it wants |
   |---|---|
   | Pokédex | every number from 1 to the highest, the highest never lower than what's stored, exactly one main form per species |
   | Numbers | base stats above 0; max CP at level 50 between 10 and 10,000 (today: Magikarp 310 to Eternamax Eternatus 9,255); the CP multipliers for every level 1–51 in steps of 0.5, each higher than the one before |
   | PvP rankings | each league has a ranking and no rank twice (PvPoke's order: a form the import doesn't have leaves a gap, which is a warning) |
   | Best IVs | CP within the league's cap, level 1–51, IVs 0–15; every main form (Pokédex 1–1,025, not a Mega or battle-only form) has best IVs in all three leagues |
   | Keeper boxes | every Pokédex number 1–1,025 has a Great and an Ultra League box, with valid bars and the forms that judged it |
   | Raid rankings | each of the 18 types has at least 10 entries, ranks 1, 2, 3… with no gaps or repeats, DPS / TDO / score above 0 |
   | Raid movesets | per form, ranks 1, 2, 3… with no gaps or repeats, DPS / TDO / score above 0 |
   | Keeper strings | every Great and Ultra League string run through the game-search simulator against the new boxes (`docs/ENGINE.md`): exactly one string matches each Pokémon inside its box under the cap, none anything else, each at most 1,950 characters. **No strings yet**: they come with the keeper filters (E04-F01), which only adds them to `ImportedGameData.KeeperFilters` |

2. **Saves everything in one transaction**, counting the changes, and lists every row the sources **no longer list**: a form no longer released, a move gone from a movepool, an evolution, a CP multiplier, a ranking entry, a best-IVs row, a keeper box, a raid ranking or moveset (types, families, species and moves are never removed). Any such row **holds the run**: it rolls back (nothing published, yesterday's data stays live) and ends with the status **Held** ("waiting for the super admin"), with what would go in the run log (the first 20 per kind, in order, with the total) and a fingerprint of all of it.
3. Otherwise **commits**: the new data is live.

**The super admin decides** a held run (the issue says how):

- **Publish**: **Run daily import** with **Publish what's missing** ticked (or Run now with `publishMissing`, super admin only). That run publishes the removals **only when they are exactly the ones the latest held run showed** (same fingerprint); if something else has gone missing since, it holds again with the new list and a warning. Its run log says `publishedMissing: true`, and who started it.
- **Keep**: do nothing. Each later run holds again while the rows are missing; a held run isn't a successful one, so after 3 days the out-of-date warning appears on the game-data screens.

**The first import into a database** (no import published yet; the dev seed's sample run doesn't count) has no published data to lose, so it publishes and still lists the removals. Small removals happen in normal updates (PvPoke's lists change entries now and then), so expect a decision now and then; the rankings could later be let through without asking.

## Run now

`POST /admin/jobs/daily-import/run` (admins; body `{"publishMissing": false}`) starts the copy's own import job straight away and answers `202` with the job execution's name; the job writes its run log as it starts (trigger `admin`, `requested_by_user_id` = who pressed it). `publishMissing: true` is for the **super admin only** (`403` for other admins). `409` while a run is starting or running: Azure is asked first whether the job has an execution starting or running (a run's own log only appears once its container has started, 20–40 seconds later), then the run log for one started in the last 35 minutes; `503` on a copy without a job (local, tests); `502` with the reason when Azure refuses. Until the Data status screen's button (E10-F01) it's tried from the API docs page on QA (`/docs`, **Copy API token** from the profile menu).

The API starts the job with **its own Azure identity** (system-assigned, switched on by `deploy_api.sh`, which also sets `RunNow__SubscriptionId`, `RunNow__ResourceGroup` and `RunNow__JobName`): it reads the job's container and starts it with the run's arguments, as `run_import.sh` does, so nothing secret passes through the API (`api/MyPoGoCompanion.Api/Admin/AzureJobStarter.cs`). The identity may only read and start that one job (**Container Apps Jobs Operator** on `job-import-<copy>`). The pipeline can't hand out that permission (it's Contributor), so **the owner runs once per copy, in Azure Cloud Shell**:

```bash
bash infra/grant-run-now.sh qa     # after a QA deploy of this version
bash infra/grant-run-now.sh prod   # after the prod deploy of this version
```

(Upload the script to Cloud Shell, or paste its contents. Running it again changes nothing.)

## The run log

Each run is a row in `job_runs` (`job = 'DailyImport'`): status (Running → Succeeded or Failed), start and finish (so the duration), and:

- `status`: Running → **Succeeded**, **Failed** or **Held** (rows missing, nothing published, the super admin decides).
- `requested_by_user_id`: who pressed Run now (null for the schedule, the deploys and the workflow).
- `summary` (JSON): `trigger` (`schedule`, `deploy`, `manual` or `admin`), `version` (the job's commit), `unchanged`, `sources` (each file's SHA-256 and size), `changes` per kind (`{"forms": {"added": 3, "changed": 1, "removed": 0}, …}`; for a held run, what would have changed), `checks` (each sanity check: `name`, `passed`, `problems`, `detail`), `removals` per kind (`{"forms": {"count": 1, "items": ["Vulpix (Alolan)"]}}`), `removalsFingerprint`, `publishedMissing`, and `warnings` (what the import couldn't use, in plain words; the run still succeeds).
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

It downloads the real files (about 27 MB) and replaces the dev seed's game data with the real one (the seed's made-up values change; its forms that the sources don't list are marked not released: the first import publishes that without holding). Settings can be passed after the command, e.g. `--DailyImport:Trigger=manual` or `--DailyImport:PublishMissing=true` after a held run. Exit code 0 = succeeded, held or nothing to do, 1 = failed, 64 = unknown command.

## Tests

- **Unit tests** (`jobs/MyPoGoCompanion.Jobs.UnitTests`): reading each file, building the game data, the English task texts, the downloader's retries, a whole run on an in-memory database (run log, nothing-changed shortcut, failures, a held run, publishing what's missing and when it doesn't apply, lock, kept fields, counts), and every sanity check on broken data (`ImportChecksTests`). The fixtures are too small for two rules (the whole Pokédex, 10 raiders per type), which the tests switch off with `DailyImport__FullPokedex=false` and `DailyImport__MinimumRaidRankingsPerType=0`. They read **fixtures**: the real files cut to 28 species that cover the tricky cases, with their game text (`Fixtures/make_fixtures.py`; regenerate with the folder of downloaded files when the sources change shape). The Portuguese names have their own tests (`GameTextTests`, `PortugueseNamesTests`): the real fixture names, the naming rules on made-up text, missing names and a missing or broken text file.
- **Engine tests**: the golden files and the engine's rows (`KeeperGoldenFilesTests`, `EngineResultsTests`; `docs/ENGINE.md`).
- **Integration tests** (`tests/MyPoGoCompanion.IntegrationTests/DailyImportTests.cs`): the job's command line on a real PostgreSQL 17 with the same fixtures: a first import and `/freshness`, the engine's stored results, a second one that changes nothing, a failed download that leaves the data, a main form that moves, the lock, an import without the game text that keeps the Portuguese names, an import on top of the dev seed (live data and collections pointing at forms), and a held run published by the super admin. Run now: `RunNowTests` (roles, through HTTP) and the API unit tests (`RunNowEndpointsTests`, `AzureImportStarterTests` with a fake Azure).
