# Calculation engine

The numbers behind the PvP IVs, the keeper filters, the raid tier lists and the raid counters (E02-F03). The code is `api/MyPoGoCompanion.Engine/`: a library that only does maths (no database, files or network), shared by the jobs and, from the screens that need it, the API. It is the C# port of the Python keeper build (`legacy/keeper-filters/`, `docs/KEEPER-FILTERS.md`) and must produce **exactly** the same results from the same files (the golden files, below).

The daily import calculates and stores its results every day (decision 2026-11-02); counters against a specific boss are calculated when asked.

## What it calculates

| Area | Code | What |
|---|---|---|
| CP | `Cp`, `CpMultipliers` | The game's CP formula `floor((Atk + a) × √((Def + d) × (HP + h)) × CPM² / 10)`, at least 10; max CP (15/15/15) at levels 40, 50 and 51; 100% CP at the catch levels 15, 20, 25, 30, 35 |
| IVs and appraisal | `Ivs`, `Appraisal` | The 4,096 IV spreads in the Python order (Attack, then Defense, then HP, 0 → 15); appraisal bars 0 = 0, 1 = 1–5, 2 = 6–10, 3 = 11–14, 4 = 15 |
| PvP | `Pvp/LeagueStats` | Every spread at the **highest level ≤ 50 under the league's CP cap** (Great 1,500, Ultra 2,500, Master none), scored by **stat product** `Atk × Def × floor(HP)`; rank and % of rank 1 for any spread (the IV rank checker, on demand) |
| Keeper boxes | `Pvp/KeeperBoxes`, `Pvp/KeeperCalculator` | Per Pokédex number and league: the appraisal bars to keep (below) |
| Raids | `Raids/RaidFormula`, `Raids/RaidRanker` | DPS, TDO and raid score of a moveset; the tier lists per attacking type with their picks; every form's movesets ranked |
| Counters | `Raids/BossCounters` | Counters against a specific raid boss, using its moves (below) |
| Types | `Types/TypeChart` | GO's type chart: super effective ×1.6, not very effective ×0.625, "no effect" ×0.390625 (GO has no immunities); dual types multiply. Screens round to ×1.6 / ×0.63 / ×0.39 |

## PvP rank 1 and ties

HP is rounded down, so spreads that differ only in HP often have the **same stat product** (about 10% of forms in Great League, 12% in Ultra, 19% in Master: 15/15/14 ties 15/15/15). Two rules:

- **Keeper boxes** keep the Python build's rule (the first spread in order wins a tie), so they match the golden files.
- **The stored best IVs** (`pvp_best_ivs`, what the screens show) give a tie to the **higher CP**, then the first in order: Master League's best is 15/15/15 at level 50.

## Keeper boxes

For each Pokédex number 1–1,025 and league (Great, Ultra):

1. **Judged by**: its final evolutions (all branches); a middle stage also judges itself when it is in PvPoke's top 150 for the league. A form that doesn't evolve judges itself. Megas, Shadows and battle-only forms are left out (Shadows have the same IVs).
2. **Keepers**: the spreads within **98%** of each judging form's rank-1 stat product, together.
3. **The box**: of the 3,375 boxes (one bar range per stat), the one with the best **F1** against the keepers (`2·TP / (2·TP + FP + FN)`) that holds every judging form's rank-1 spread; on a tie the box with the fewest spreads, then the first.

## Raid score

GamePress' "comprehensive DPS", as the Python build computes it:

- **Attacker**: level 40, 15/15/15; Attack and Defense × the level's CP multiplier (Shadow: Attack × 1.2, Defense × 5/6); HP rounded down.
- **Bonuses**: same type ×1.2; a Mega ×1.3 on its own types, ×1.1 on the others.
- **Damage per hit**: `floor(½ × power × Atk ÷ target Def × bonuses) + 1`.
- **The fight**: fast moves and the charged move whenever there is energy; energy also from damage taken; a 100-energy move wastes some. **DPS**, **TDO** (total damage before fainting) and **score = (DPS³ × TDO)^¼** (damage per second counts three times as much as staying power). A charged move worse per second than the fast move is not used.
- **Tier lists** (`raid_rankings`): against a neutral target with Defense 180 that deals 900 ÷ Defense damage per second; each released form (and Shadow) with its best moveset whose fast and charged moves are both of the type; Return and Frustration left out. The picks: the first 10 Pokédex numbers (Megas aside) and every Mega scoring above the 10th.
- **Moveset ranking** (`raid_movesets`): every fast + charged combination of a form against the same neutral target, best first (Return and Frustration included here).

## Counters against a boss

Decision 2026-11-02 (option A):

- **The boss**: (base + 15) × its tier's CP multiplier (1★ 0.5974, 3★ 0.73, 5★, Mega, Legendary Mega and Elite 0.79; Shadow bonus for a Shadow boss).
- **The counter's hits** use its type effectiveness against the boss's types, same type, Shadow and Mega.
- **The damage it takes** comes from the boss's real moves and the counter's own types: the boss uses its fast move every move length + 2 s and its charged move when it has the energy (from its fast moves). A Mega boss gets no Mega bonus. That damage per second replaces the neutral target's in the same formula, which gives **damage/s**, **how long it lasts** (HP ÷ damage taken per second) and **% of best**.
- **Averaged over the boss's moves**: with no moves picked, every fast + charged combination of the boss counts equally and counters are ranked by the average score, each with its best moveset on average. Picking the boss's moves uses only that combination.
- Counters are level 40, 15/15/15, released forms, Shadows included, Elite TM and Legacy moves included (flagged); no weather, friendship or dodging. The page shows 9 with **one Mega slot** (`WithOneMega`).

## What the daily import stores

From the same parsed files as the rest of the import (`jobs/.../DailyImport/EngineInputs.cs`, `EngineResults.cs`), in the same transaction:

| Table | Rows (real files, Nov 2026) |
|---|---|
| `pvp_best_ivs` | 3,603: every non-Mega form × Great, Ultra, Master |
| `pvp_keeper_boxes` | 2,050: Pokédex 1–1,025 × Great, Ultra (`judged_by` lists the forms) |
| `raid_rankings` | 2,310: per attacking type, rank 1 = best, Megas included |
| `raid_movesets` | 27,298: every form (and Shadow) × fast × charged |

Rows for a form or move the import doesn't keep are left out. The calculations take about 2 seconds; the whole import about 20.

## Golden files

`jobs/MyPoGoCompanion.Jobs.UnitTests/KeeperGoldenFilesTests.cs` reads the frozen inputs (`tests/golden/keeper-filters/inputs/`) with the import's own readers, runs the engine and compares with the Python outputs **exactly**, with no tolerance:

- `pvp_boxes.json`: every Pokédex number's box, F1 (rounded to 4 digits like Python's `round`), counts, judging forms and their rank-1 spreads, for Great and Ultra League.
- `raiders.json`: every scored entry per type with its moveset, DPS, TDO, score, every intermediate number and the picks.

The search strings built from the boxes (`pvp_strings.json`, `data_*.json`) and the search simulator (`searchsim.json`) are ported with the keeper filters (E04-F01) and the verification step (E02-F04).

## Tests

- `api/MyPoGoCompanion.Engine.UnitTests`: the maths on real numbers (CP, ranks and ties, boxes, policies, raid formula, rankings and picks, counters and the boss's moves, type chart) and every error.
- `jobs/MyPoGoCompanion.Jobs.UnitTests`: the golden files, and the import's rows from the fixtures (`EngineResultsTests`).
- `tests/MyPoGoCompanion.IntegrationTests/DailyImportTests.cs`: the stored results on a real PostgreSQL, the same numbers as the golden files for Charizard, and a second run that changes nothing.
