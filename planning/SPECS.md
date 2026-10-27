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

## 3. Moves — signed off 2026-10-27

**What it's for.** Look up any move: how strong it is in raids and in PvP, who can learn it, and which moves are best for each type.

**Getting there.** From the menu (Moves) or by tapping a move on any Pokémon page.

**Moves list (MVP).** Every fast and charged move, filterable by type and searchable by name, with two sets of numbers because the game uses different values in raids and in PvP: raids (power, duration, energy — e.g. Dragon Tail 14, 1.0 s, +8) and PvP (power, energy, turns — e.g. Dragon Tail 9, +12, 3 turns). Sortable by any number.

**Move page (MVP).** Its numbers and every Pokémon that can learn it, in three groups: normally (TMs), Elite TM only, legacy. Tapping a Pokémon opens its page.

**Type chart (MVP).** The full attacking × defending chart; pick a dual type (e.g. Water + Ground) to see combined weaknesses (Grass ×2.56) and resistances.

**Best movesets per Pokémon (MVP).** On each Pokémon page: every fast + charged combination ranked for raids and PvP.

**Later (Good to have).** Move history (rebalances over time); Max and G-Max move power by level; move rankings per type (raids: damage/energy per second; PvP: per turn); TM advice on your Pokémon (which of yours lack their best moves and which TM to use).

**Where the information comes from.** Game files (move numbers, who learns what), PvPoke (Elite TM and legacy flags), our calculations (rankings).

## 4. Search filters — signed off 2026-10-27

**What it's for.** Search strings you paste into the game's storage search to find exactly the Pokémon you want (to tag, keep, or transfer the rest). The app does the analysis the game can't.

**Getting there.** From the menu (Filters); family strings from Pokémon pages.

**Ready-made keeper filters (MVP).** Today's Keeper Filters, rebuilt daily: Great League and Ultra League (every Pokémon whose appraisal bars sit in its high-ranking zone, pre-evolutions judged by their evolutions; all 1,025 Pokémon; 2 strings for GL, 3 for UL), raid attackers (top attackers per type, their Megas and pre-evolutions, Attack 11–15 or 15 only), Max Battle attackers (Dynamax/Gigantamax with high Attack), Megas (all Mega-capable plus announced ones not yet in GO). Each with a Copy button and how-to (paste, select all, tag, search `#TAG` later).

**"Safe to transfer" (MVP).** The inverse: not a keeper for any league, raids, Max or Megas; shinies, luckies, legendaries and favourites excluded, with a safety note.

**Family strings (MVP).** A short string per family on each Pokémon page.

**Filter builder (MVP).** Pick conditions from lists (Pokédex numbers/names, types, appraisal bars per stat, CP range, stars, shiny, lucky, Shadow, legendary, distance, age, other game keywords); the string updates live and can be edited by hand; the app checks it and splits strings over ~1,950 characters.

**Saved filters (MVP).** Name, save, reopen and copy filters.

**Later (Good to have).** Builder options (keeper margin 95/98/99%, level cap 50/51); explain a string (plain words + matches); preview matches on your collection; utility library curated by the admin.

**Future.** Cleanup routine (filters with "last applied" dates and new matches per import).

**Where the information comes from.** Game files, PvPoke rankings, our calculations; every string verified by simulating the game's search before publishing.

**Limits.** English keywords (the owner's game language); forms sharing a Pokédex number can't be told apart by the game.

## 5. Tier lists — signed off 2026-10-27

**What it's for.** See at a glance which Pokémon are best for each activity, to know what to catch, keep and power up.

**Getting there.** From the menu (Tier lists) and from the rankings on each Pokémon page.

**PvP tier lists (MVP).** Great, Ultra and Master League from PvPoke, updated daily: Pokémon, best moveset, score, grouped into S/A/B/C tiers. Tapping an entry opens its page.

**Raid tier lists (MVP).** Per attacking type: best attackers with best moveset, DPS, total damage and score (today's numbers table). An overall view (top attackers across types, best per type). "Your best Mega": pick your Mega to pin it as the Mega slot and rank the rest without other Megas.

**"Only what I own" (MVP).** With the collection imported, show only Pokémon you have, with your copies' ranks.

**Later (Good to have).** Max Battle lists (attackers, tanks, healers; estimates at first); your gaps & what to build ("2 of the top 10 Ground attackers; next: Shadow Excadrill"); "why it's ranked here" for PvP and Max lists.

**Future.** Cup lists (Little Cup and rotating cups); budget picks; risers & fallers.

**Where the information comes from.** PvPoke (PvP, MIT, credited); our damage calculations (raids: level 40, 15/15/15, formula shown); our Max Battle model (Max).

## 6. Raid bosses — signed off 2026-10-27

**What it's for.** Know what's in raids now and what's coming, and how to beat each boss.

**Getting there.** From the menu (Raids), "Today in the game" on the home page, and the calendar.

**Boss list (MVP).** Current bosses by tier (1-star, 3-star, 5-star, Mega, Shadow, Elite) with picture, types, dates and shiny availability; an Upcoming section. Fetched weekly by the AI researcher (sources such as LeekDuck) and published automatically; the admin can fix or remove entries.

**Boss page (MVP).** Types, weaknesses and resistances; possible moves; 100% CP normal and weather-boosted; **best counters** (averaged over the boss's moves, re-ranked when you pick its actual moves; one Mega slot); **your best counters** from your collection at real levels and IVs.

**Catch-screen IV check (MVP).** Type the catch-screen CP to see the possible IVs (raid floor 10/10/10).

**Later (Good to have).** Tips per boss (admin-written); estimated minimum players (raid simulator, labelled estimate); counter search string; "can I do it with my team?".

**Future.** Weather check (approximate in-game weather where you are).

**Where the information comes from.** AI researcher (bosses, dates; source shown), game files (boss stats, moves), our raid calculations (counters, with type effectiveness and boss moves).
