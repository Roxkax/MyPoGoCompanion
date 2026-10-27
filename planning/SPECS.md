# MyPoGoCompanion — signed-off feature descriptions

How each feature works from the user's side, as signed off by the owner. Priorities are in `planning/PRIORITIES.md` (MVP / Good to have / Future); technical notes in `PLAN.md`.

## 1. Pokédex — signed off 2026-10-27

**What it's for.** Look up any Pokémon in the game and find the ones that match what you need — for example "every Ground type with Attack above 250" or "all Legendaries not released yet".

**Opening it (MVP).** A list of every Pokémon: all 1,025 species, and each version that plays differently gets its own line — Charizard, Mega Charizard X, Mega Charizard Y and Shadow Charizard are four lines; Vulpix and Alolan Vulpix are two. Each line shows the picture, Pokédex number, name, types and a few key numbers (such as base stats and max CP; exact columns decided in design). Starts in Pokédex order. Tapping a line opens the Pokémon's page (feature 2).

**Searching (MVP).** One box that filters as you type: names in English or Portuguese ("Garchomp", "garch"), Pokédex numbers ("445"), and common nicknames ("chomp", "m2", "zard").

**Filtering (MVP).** Combinable filters: type (one or two), generation, stats (min/max Attack, Defense, Stamina, max CP), variants (Shadows, Megas, can Dynamax/Gigantamax), category (Legendary, Mythical, Ultra Beast), released in GO or not.

**Sorting (MVP).** Tap any column to sort; tap again to reverse.

**Later (Good to have).** Move filters ("can learn Earth Power", "has an Elite TM move"); availability filters ("shiny available", "in raids right now", from the AI researcher's data); "I own it" markers with shiny/lucky/hundo/Shadow badges and owned/missing filters (needs the collection); collection tracker with progress per region and category; compare 2–4 Pokémon side by side.

**Future.** Saved views (a saved filter + sort, reopened in one tap).

**Where the information comes from.** Game files (stats, forms, moves, released), downloaded and checked daily; PvPoke and our raid calculations (rankings); the game's text files (Portuguese names); the game's sprites (pictures). Nothing is entered by hand.

**Limit.** Forms that share a Pokédex number are shown separately here, but the game's search strings can't tell them apart.

## 2. Pokémon page — signed off 2026-10-27

**What it's for.** Everything about one Pokémon on one page: how strong it is, what it learns, how to get and evolve it, whether yours is any good, and what it's useful for.

**Getting there.** Tap a Pokémon anywhere (Pokédex, tier lists, raid counters, search). Each form has its own page (Garchomp, Mega Garchomp, Shadow Garchomp) with quick links between them.

**At the top (MVP).** Picture, name, number, types; base Attack, Defense, Stamina as bars; maximum CP at levels 40, 50 and 51.

**100% CP cheat sheet (MVP).** The CP of a 15/15/15 in each situation: research (L15), raids and eggs (L20), weather-boosted raids (L25), wild (L30; L35 when boosted). E.g. Garchomp: 2,264 from a raid, 2,830 boosted.

**Moves (MVP).** Every fast and charged move with raid numbers (power, duration, energy) and PvP numbers side by side; Elite TM, legacy and event-only labels; every fast + charged combination ranked for raids and for each PvP league.

**Evolution (MVP).** The family chain with what each step needs: candy, items, buddy distance, free evolution after a trade, day/night, lure, gender, special tasks.

**How to get it (MVP).** Permanent ways (wild, egg distance, raid tier, research, evolving, trading, regional areas) **and where it appears right now** (current raids, eggs, research and events, from the AI researcher).

**Versions (MVP).** Regional forms and costumes (including retired), and which shiny, Shadow, Purified, Mega, Dynamax and Gigantamax versions exist.

**Rankings (MVP).** Rank in Great, Ultra and Master League, raid rank for its types, Max Battle rank — each linking to the full tier list.

**Best IVs (MVP).** For each league: the ideal IVs, the appraisal bars to keep, and a Copy button for a search string for its family. An **IV rank checker**: type any IVs to see rank, best level and CP per league, for it and its evolutions.

**Type matchups (MVP).** Weaknesses and resistances with multipliers.

**Sources (MVP).** Each fact shows where it came from (game files, PvPoke; later "admin-verified").

**Later (Good to have).** Special conditions found by the AI (with data corrections); changes over time (history from the game files); "report wrong data"; your copies of it with their ranks; power-up cost (candy, XL candy, Stardust, incl. Shadow/Purified/lucky).

**Where the information comes from.** Game files (stats, moves, evolution rules, forms, history), PvPoke (PvP rankings, move flags), our calculations (raid ranks, best IVs, CP tables), the AI researcher (current appearances), later AI-found facts.
