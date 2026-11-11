# Pokémon page (B2, E03-F02)

One species with all its versions: how strong it is, what it learns, whether a copy is any good, how to get it and
evolve it, and every version it has. Decisions: [project context](PROJECT-CONTEXT.md#decision-log), decision log 2026-11-08.

## Address

`/pokedex/<name>?version=<version>#<tab>`

- **name**: the species' English name in lowercase, spaces as `-`, accents dropped (`garchomp`, `mr-mime`,
  `flabebe`); the number works too (`/pokedex/445`). The app finds the number in the Pokédex list it already has
  (`GET /pokedex`, kept an hour). An unknown name shows "There is no Pokémon called … in the Pokédex".
- **version**: a form id (`garchomp_mega`, `vulpix_alolan`), or a form id with `_shadow` for its Shadow
  (`garchomp_shadow`). Left out for the main form; an unknown version shows the main form.
- **tab** (phone only): `#moves`, `#ivs`, `#getIt`, `#versions`; none for Overview. Back returns to the last tab.

## Data: one answer per species

`GET /pokedex/{dex}` (public, `api/MyPoGoCompanion.Api/Pokedex/PokemonPage*.cs`) sends every version of the species
with everything the page shows, so switching versions or tabs asks the API nothing:

| Part | From |
|---|---|
| Stats, max CP at L40/50/51, the 100% CP cheat sheet (L15–35), type matchups | forms + the engine (`Cp`, `TypeChart`) |
| Movepool with Elite TM / Legacy, every move's raid and PvP numbers | `form_moves`, `moves` |
| PvP rank, score, recommended moveset and **move usage** per league (and Shadow) | `pvp_rankings`, `pvp_move_usage` (PvPoke) |
| Best IVs per league (Megas too) and the keeper bars of the Pokédex number | `pvp_best_ivs`, `pvp_keeper_boxes` |
| Raid rank per attacking type (with its moveset) and every raid moveset ranked | `raid_rankings`, `raid_movesets` |
| The family (every non-Mega form of every species in it) and its evolution steps | `species`, `forms`, `evolutions` |
| Costumes | `costumes` (PokeMiners' picture list) |
| Appearances: raids, Max Battles, eggs, research, events, with their times | the live data (AI researcher); entries the admin removed are left out |
| CP multipliers (for the IV checker) | `cp_multipliers` |

Each built answer is kept in memory, compressed (Brotli and gzip), until a daily import or an AI researcher run is
published (at most 300 species; the oldest built go first). Browsers keep it an hour (`Cache-Control:
public, max-age=3600`), then ask again with its ETag and get `304 Not Modified` while nothing changed. About 13 KB
of JSON for Garchomp on the development sample.

## The page

Header (tinted with the main type): picture, number, name, types and the **version switcher**: each form with its
own stats (looks-only forms are on the Versions tab), and its Shadow when released. The Shadow has the same stats
and its own PvP and raid rankings and raid movesets.

| Phone tab | Desktop column | Sections |
|---|---|---|
| Overview | left | **Stats** (bars, max CP L40/50/51) · **Rankings** (Great/Ultra/Master League, raids for each of its types; Shadow and Mega ranks under them; each links to its tier list; no Max Battle rank before the Max Battles features) |
| Overview | right | **Type matchups** (weak to, resists, with GO's multipliers; link to the type chart) |
| Moves | left | **Moves**: best moveset for raids per own type and per PvP league (leagues with the same one joined), then the movepool with raid numbers (power, duration, energy) and PvP numbers (power, energy, turns) · **Raid movesets** (every combination: damage/s, total damage, % of the best; 8 shown, "Show all") · **PvP moves** (PvPoke's recommended moveset and each move's share of its kind in PvPoke's simulations, per league) |
| IVs | right | **Best IVs** per league with "Keep: Atk 0–5 · Def 11–15 · HP 6–15" (the keeper bars; none for Megas or Master League) · **Check any IVs** · **100% CP cheat sheet** |
| Get it | right | **How to get it**: right now (and coming up) from the live data, then the history, newest first · **Evolution** chain with each step's needs, and the buddy distance |
| Versions | left | Every version's picture (shiny only once known, Shadow, Purified, Mega, G-Max/Dynamax), the shown version's numbers, and the costumes |

The search string with Copy on the Best IVs cards comes with **E04-F03 Family strings** (decision 2026-11-08).

## IV rank checker

Calculated in the browser (`web/src/pokemon/page.ts`, the engine's `LeagueStats` in TypeScript): for each of the
4,096 spreads, the highest level up to 50 whose CP fits the league, its CP and stat product
(`Atk × Def × floor(HP)` at that level). A spread's rank is 1 + the number of spreads with a higher stat product
(equal ones share a rank); its share is its stat product over rank 1's. Rows within 98% of rank 1 are coloured (the
keeper filters' rule). Shown for the version and each Pokémon it evolves into. Unit tests check it against the stored
best IVs (Garchomp: Great League 0/15/14, level 14, CP 1,495).

## Out-of-date warning

The page shows game data and live data, so both warnings can appear on it (raids and events first).

## Tests

- API: `api/MyPoGoCompanion.Api.UnitTests/Pokedex/PokemonPageEndpointsTests.cs`,
  `tests/MyPoGoCompanion.IntegrationTests/PokemonPageTests.cs` (and the OpenAPI checks).
- Web: `web/src/pokemon/page.test.ts`, `web/src/screens/pokemon/PokemonScreen.test.tsx`.
- UI: `tests/ui/specs/pokemon.spec.ts` (behaviour only: QA has real data, local runs the development sample).
