# Database

The app's PostgreSQL 17 database (Neon in QA and prod, Docker locally). The schema is the EF Core model in `api/MyPoGoCompanion.Data/` and is changed only through its migrations (`dotnet ef migrations add`). Schema v1 (E01-F01, migration `SchemaV1`) covers what the MVP features need; later features add their own tables and columns with their own migrations.

## Conventions

- **Names** are snake_case (`collection_pokemon.attack_iv`); constraints are `pk_`, `fk_`, `ix_`, `ck_` + table. The one exception is EF's own history table, `__EFMigrationsHistory`, which keeps `"MigrationId"` and `"ProductVersion"` (`MigrationHistory.cs`): QA and prod created it before snake_case names, and renaming its columns stops every later deploy from reading which migrations ran.
- **Keys.** Game data keeps the game's own ids, so the daily import can update rows in place: types `ground`, moves `EARTH_POWER`, families `FAMILY_GIBLE`, forms PvPoke's ids (`garchomp`, `garchomp_mega`, `vulpix_alolan`), species their Pokédex number. Everything else uses time-ordered UUIDs (version 7), created by the app.
- **Enums** are stored as their names (`FiveStar`, `Shadow`), so the data reads without the code.
- **Moments** (when something happened: uploads, job runs, fixes) are `timestamp with time zone`, in UTC.
- **Game schedule times** (raids, Max Battles, events) are wall-clock times, `timestamp without time zone`, because most of the game runs on local time everywhere (raids rotate at 10:00 local, Community Day is 14:00–17:00 local). `time_zone` is null for those; a global moment (a worldwide launch) stores its IANA zone there. The API turns both into the user's time. In C# these are `DateTime` values with `DateTimeKind.Unspecified`; Npgsql refuses UTC ones.
- **Portuguese text** is a `_pt` column next to the English one (`name_en`, `name_pt`). Game text that the import hasn't found yet is null and the app falls back to English. A third language would add a column per text.
- **JSON** (`jsonb`) only for things read whole: what a source said, a filter's builder conditions, a Pokémon before and after an upload, a job's change counts.
- **Check constraints** keep impossible values out: IVs 0–15, levels 1–51 in half steps, stars 1–6, appraisal bars 0–4, one default form per species, an end after its start, languages `en` / `pt-PT`.

## Tables

### Game data (daily import)

| Table | One row per | Notes |
|---|---|---|
| `types` | type (18) | English and Portuguese name, the game's order |
| `families` | evolution family | `FAMILY_GIBLE`; candy is per family |
| `species` | Pokédex number (1,025) | name, generation, Legendary/Mythical/Ultra Beast, family, search nicknames ("chomp") |
| `forms` | version with its own stats, types or picture | base form (exactly one per species, `is_default`), regional forms, Megas, Primals, other battle forms, costumes; stats, types, max CP at level 50, released, Shadow released, shiny, can Dynamax / Gigantamax, buddy distance, second-move cost. **Shadow, Purified, Dynamax and Gigantamax are not rows**: same stats, so they are flags here and an `is_shadow` column where rankings differ |
| `moves` | fast or charged move | raid numbers (power, duration, damage window, energy) and PvP numbers (power, energy, turns, stat changes) side by side |
| `form_moves` | move in a form's movepool | `Normal`, `EliteTm` or `Legacy` |
| `evolutions` | evolution step | candy (and after a trade), item, buddy distance, day/night, lure, gender, special task |
| `cp_multipliers` | level (1–51) | the game's CP multiplier |

### Rankings and calculated data (daily import)

| Table | One row per | Notes |
|---|---|---|
| `pvp_rankings` | league × form × Shadow | PvPoke's rank, score and recommended moveset (tier lists, Pokémon page) |
| `pvp_best_ivs` | league × form | the rank-1 IVs, level, CP and stat product. **Only rank 1 is stored**; the IV rank checker and "your copies" rank all 4,096 spreads on demand (about a millisecond) |
| `pvp_keeper_boxes` | league × Pokédex number | the appraisal bars the keeper filters keep, and the forms that chose them |
| `raid_rankings` | attacking type × form × Shadow | best same-type moveset, DPS, TDO, score and rank (raid tier lists, best teams); Megas included, the lists keep one Mega slot |
| `raid_movesets` | form × Shadow × fast + charged move | every combination ranked for raids (Pokémon page) |
| `keeper_filters` | filter × option × part | the ready-made search strings (Great League 2 parts, Ultra League 3, raid, Max, Mega), checked by the search simulator before they are published |

Boss counters are calculated on request from these tables and the boss's moves, so they are not stored.

### Live data (weekly AI researcher)

| Table | One row per | Notes |
|---|---|---|
| `raid_bosses` | boss in a rotation | form, Shadow, tier, start/end, shiny, event |
| `max_bosses` | Max Battle boss | form, stars 1–6, Gigantamax, start/end, shiny, event |
| `events` | calendar entry | kind (Community Day, Spotlight Hour, Raid Hour, Raid Day, Max Battle, season, special), name and summary, start/end |
| `event_pokemon`, `event_moves`, `event_bonuses` | featured Pokémon, exclusive move, bonus line | deleted with their event |
| `egg_pool_entries` | Pokémon in an egg pool | egg kind (2–12 km, Adventure Sync, Route), shiny, rarity, event |
| `research_tasks`, `research_rewards` | Field Research task and its Pokémon rewards | event research links to its event |

Every live-data row carries the researcher's stable `key` (so a later run updates it instead of adding a copy), the source page (shown to the admin only), what the source said last time (`source_snapshot`), when it was first and last fetched and last changed (the admin's "New" and "Changed this week"), and the admin's fix: `fixed_at` / `fixed_by_user_id` lock it against the researcher, `source_changed_at` is set when the source changes after the fix ("Source changed since your fix"), `removed_at` hides it and stops the researcher adding it back.

### Users and their data

| Table | One row per | Notes |
|---|---|---|
| `users` | Google account | Google id, email, name, picture; language and theme; role (user or admin, with who granted it and when). The super admin is not stored: it comes from configuration |
| `saved_filters` | saved search string | name, string, the builder's conditions (JSON) |
| `collection_imports` | Poke Genie upload | status (for the progress steps), counts for the report, error |
| `unrecognised_rows` | row that didn't match | row number, name, form, CP, reason |
| `collection_pokemon` | Pokémon in the current collection | form, Shadow/Purified, CP, IVs, level, moves, shiny, lucky, favourite, gender, catch date, the upload it came from |
| `collection_changes` | difference from the previous upload | new / gone / changed, what changed, the values before and after (JSON) |

**The uploaded file is not kept**: its rows become `collection_pokemon`, unmatched rows are kept with their reason, and the differences are kept per upload, so the upload history is those diffs.

Sign-in tokens get their own table with the sign-in feature (E01-F02).

### Running the system

| Table | One row per | Notes |
|---|---|---|
| `job_runs` | run of the daily import, AI researcher or backup | status, start/end, scheduled or "Run now" (by whom), what changed, error. The out-of-date warning reads the last successful run of each job |
| `ai_usage` | AI call | chat or researcher, model, tokens, cost; this month's sum is checked against the cap. Chat messages are not stored |
| `admin_fixes` | admin fix or removal | who, when, which entry, before and after |

## What deleting does

- **A user**: their saved filters, uploads (with unrecognised rows and changes) and collection go with them. Records that only mention them stay, with the user emptied: AI usage, job runs they started, entries they fixed and the audit of their fixes.
- **Game data**: inside game data, rows cascade (a deleted form takes its movepool, evolutions and rankings). A form or move that live data or a collection points at **can't be deleted**: the import marks it (not released) instead.
- **An upload**: refused while the collection still comes from it.
- **An event**: its featured Pokémon, moves and bonuses go with it; raids, eggs and research that belonged to it stay, unlinked.

## Size

Neon's free tier holds 0.5 GB. Game data and rankings come to a few tens of MB (the largest is `raid_movesets`, roughly 50,000 rows); a collection of 5,000 Pokémon is about 1 MB, and an upload's changes a few KB. Storing every PvP IV spread instead (4,096 per form and league, about 21 million rows) would not fit, which is why only rank 1 is stored.

## Development data

Locally, the API (in `Development`) applies the migrations on start-up and, when the database is empty, adds the design's sample dataset (`design/source/CONVENTIONS.md`): `api/MyPoGoCompanion.Data/Seed/`.

- **Game data and rankings** (`dev-game-data.json`): a real slice for the 91 species in the sample, cut from the golden-file inputs and outputs by `make_dev_game_data.py`. Made up for development only: evolution candy (the usual pattern), PvP scores and movesets (the frozen rankings keep only the order), Dynamax and shiny flags, and Portuguese names other than types.
- **Live data and the trainer** (`DevSeed.cs`): the sample's raids, Max Battles, events, eggs and research, Ash_PT with 15 Pokémon, an upload report, saved filters, job runs and AI usage.
- **Dates follow the clock**: everything moves by whole days so that "now" is always the sample's Tuesday 27 October 2026, 18:30 (Kyogre in raids, Halloween live, Community Day: Gible next).

QA and prod are never seeded: their data comes from the daily import, the AI researcher and real users. To start again locally: `docker compose down -v`, `docker compose up -d --wait`, then run the API.
