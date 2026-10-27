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

## 7. Max Battles — signed off 2026-10-27 (all after the MVP)

**What it's for.** Prepare for Dynamax and Gigantamax battles: current Max bosses, which Pokémon to bring, in which role.

**Getting there.** From the menu (Max Battles), the home page and calendar during Max Battle weeks.

**Max boss list (Good to have).** Current Max bosses by star level (1–6, Gigantamax) with types and dates, fetched by the AI researcher.

**Max boss page (Good to have).** Best counters (Max model, labelled estimates); your best Dynamax/Gigantamax Pokémon from your collection; a suggested team by role — attacker (Max Strike), tank (Max Guard), healer (Max Spirit); tips (admin-written); catch-screen IV check.

**Future.** Max Move upgrade cost (candy and Max Particles for Max Attack, Guard, Spirit).

**Where the information comes from.** AI researcher (bosses, dates), game files (stats, moves), our Max Battle model (counters, roles).

**Honest note.** Max mechanics are less documented; results are labelled estimates and improve over time.

## 8. Battle simulator — signed off 2026-10-27 (all after the MVP)

**What it's for.** Test a raid before doing it: how long it takes, how many of your Pokémon faint, how many trainers you need.

**Getting there.** From a raid boss page ("Simulate"), the Raids menu, and saved teams.

**Raid simulator (Good to have).** Choose a boss and its moves (or average), a team of six (suggested, your own, or hand-picked), attacker level (40/50/51), weather and friendship. The simulation plays the battle second by second (boss attacks, fast/charged moves, energy, faints, re-lobbies) and reports time to win against the timer, faints, win/lose (e.g. "4 trainers, level 40: win in 2:35, ~3 faints each"). **Group raids**: set the number of trainers to find the minimum players — this feeds "estimated minimum players" and "can I do it with my team?" on raid boss pages.

**Future.** Max Battle simulator (once mechanics are better documented); battle timeline (step-by-step replay); saved simulations.

**Not included.** No PvP simulator (PvP rankings come from PvPoke). Dodging and Party Power aren't modelled.

**Where the information comes from.** Game files (stats, moves); our simulation model (labelled estimate).

## 9. My collection — signed off 2026-10-27

**What it's for.** Your own Pokémon in the app, so everything becomes personal.

**Getting there.** From the menu (Collection); sign-in required (saved to your account).

**Importing (MVP).** Export a CSV from Poke Genie → Upload → the app reads every Pokémon (species, form, CP, IVs, level, moves, Shadow/Purified, lucky, favourite, catch date) and matches it to the Pokédex → a report ("2,148 imported, 3 not recognised") listing unmatched rows.

**Updating (MVP).** Upload a newer export: it becomes the current collection and the app shows what changed (new, gone, powered up, evolved, new moves).

**Collection list (MVP).** Searchable, sortable (CP, IVs, level, catch date, rank). Each Pokémon shows PvP value (rank in GL/UL/ML with its IVs, best level and CP) and raid value (vs the top attackers of its type at its level). Tapping opens its Pokémon page with your copy's details.

**Used everywhere (MVP).** Feeds "only what I own" (tier lists), "your best counters" (raid bosses), "best team from your collection".

**Later (Good to have).** Suggestions (power up, safe to transfer, missing); collection stats (types, shinies, luckies, hundos, Shadows, completion); duplicates review.

**Future.** Import history (every upload kept).

**Limits.** Only as current as your last export; the app never connects to the game account (Niantic's rules); candy isn't in the export.

**Privacy.** Stored only in your account; removed with account deletion (when it arrives).

## 10. Best team — signed off 2026-10-27

**What it's for.** Know which 6 Pokémon to bring to a raid without working it out yourself.

**Getting there.** From the Raids menu (Teams), each raid boss page, and the tier lists.

**Best team per type (MVP).** Pick an attacking type → the 6 best attackers: five regular/Shadow plus one Mega (only one Mega fits), each with moveset and score.

**Best team against a boss (MVP).** On a raid boss page, the counters become a ready team of 6 (best Mega + five best others), using the boss's actual moves if picked.

**From your collection (MVP).** The same teams built only from your Pokémon at real levels and IVs; the app says when a slot has no good option.

**Mega choice (MVP).** Lock the Mega you'll actually use; the rest is rebuilt around it.

**Later (Good to have).** Save teams.

**Future.** PvP team builder (3 Pokémon, type coverage and threats, from PvPoke data).

**Where the information comes from.** Our raid damage calculations; your collection.

## 11. AI assistant — signed off 2026-10-27

**What it's for.** Ask any question about the game in your own words (English or Portuguese) and get an answer based on real data.

**Getting there.** A chat button on every page; sign-in required (the AI has a cost).

**Asking questions (MVP).** Answers from our own data — Pokédex, moves, rankings, raid bosses and counters, calendar — showing which data it used (e.g. "best non-legendary Ground attackers?", "when does Kyogre leave raids?", "Qual é o melhor moveset do Garchomp para raids?"). If our data doesn't have the answer, it says so instead of guessing.

**Cost control (MVP).** Simple questions use a smaller model; a **$5/month** cap shared with the AI researcher; when reached, the chat pauses until next month and says why.

**Later (Good to have).** Web search with cited sources (findings can become correction proposals with feature 12); saved conversations (in the MVP a chat lasts for the session); knows your collection; knows the current page; "ask about this" buttons.

**Future.** Does things for you after confirmation (filters, teams, simulations); screenshot questions.

**Where the answers come from.** Claude (Anthropic) reading our data through the API's tools; later the web, always with sources. The AI never changes data by itself.

## 12. Data corrections — signed off 2026-10-27 (all after the MVP)

**What it's for.** Enrich and fix the data with facts the game files don't contain; the AI and users suggest, only the admin decides.

**Sources of suggestions (Good to have).** The AI chat (web findings our data lacks — e.g. "Galarian Articuno only from Daily Adventure Incense"); the AI researcher (facts found while fetching bosses/events); "report wrong data" on any fact.

**Review queue (Good to have).** Cards showing the Pokémon/move and field, current vs proposed value, sources (at least one link required), AI confidence (low marked), link to the chat. Actions: approve, edit then approve, reject (optional reason).

**After approval (Good to have).** "Admin-verified" label on the Pokédex; stored in a separate layer that survives daily imports; conflicts flagged when an import changes the same field; full per-field history with revert; audit log.

**Pending alerts (Good to have).** In-app and browser notifications; push with the mobile apps.

**In the MVP (without this feature).** The chat never changes data; AI-researched bosses and events publish automatically and can be fixed with the small editor (14.12); special conditions wait for this feature.

## 13. Game calendar — signed off 2026-10-27

**What it's for.** Know what's happening in the game today and in the coming weeks, fetched by the system.

**Getting there.** The home page opens on it; also in the menu (Calendar).

**Home page "Today in the game" (MVP).** Raids now (by tier, linking to counters), Max Battles now, events live now (with end time) and next (e.g. "Mega Chandelure debut — Sat 31 Oct").

**Calendar (MVP).** Current and upcoming raid rotations, Max Battle weeks and events (Community Days, Spotlight Hours, Raid Days/Hours, seasons, special events) with dates and times in your time zone, featured Pokémon, bonuses and exclusive moves.

**AI researcher (MVP).** Weekly, reads sources such as LeekDuck and game news; fetches raid rotations, events, egg pools and research rewards; publishes automatically with the source shown. The admin can fix or remove entries, or run it again mid-week. Its cost counts in the $5/month AI cap.

**Later (Good to have).** Past events archive (feeds "when it appeared" on Pokémon pages).

**Future.** Event prep checklist; calendar sync (Google/Apple); reminders.

**Honest note.** Weekly fetching can miss mid-week surprises; an extra run can be triggered any time and the frequency raised later.

## 14. Admin panel — signed off 2026-10-27

**What it's for.** The owner's control room: keep the system healthy and the data right without touching code. Admin role only.

**Getting there.** An "Admin" menu entry visible only to admins.

**MVP — two screens.** (1) **Data status & re-run**: last daily import and weekly AI researcher runs, success/errors, what changed, a "Run now" button for each. (2) **Fix fetched entries** (14.12): bosses, Max bosses and events published by the AI researcher, with source links; edit details or remove.

**Simple in the MVP.** Admin role set in configuration (no user screen); the $5 AI cap is a configuration setting (spend visible); "Megas not yet in GO" stays in configuration or is updated by the AI researcher.

**Later (Good to have).** Review queue (feature 12); direct data edits (logged); hand-maintained lists on screen; AI usage and cap on screen; audit log; users & roles; content editor (EN/PT); health & limits; downloadable backups; feature switches.

## 15. Account and settings — signed off 2026-10-27

**What it's for.** Sign in so your collection, saved filters and AI chat belong to you; set the app up your way.

**Signing in (MVP).** "Sign in with Google", no passwords. Without signing in you can use the Pokédex, moves, filters, tier lists, raid bosses and calendar; uploading a collection, saving filters and the AI chat need sign-in. The owner is the admin. You stay signed in on a device until you sign out.

**Settings (MVP).** Language: English or Português (Portugal) — the whole interface, including Pokémon and move names (from the game's pt-BR text); search strings stay in English. Theme: light, dark, or follow the device.

**Later (Good to have).** Calculation defaults (attacker level 40/50/51, Best Buddy, include Shadows/Megas); preferred league; trainer profile (level — caps power-up suggestions —, team, friend code); delete account & download data (required before any public launch); Sign in with Apple (with the iOS app).

**Future.** Notification settings.

**Privacy.** Only the Google name, email and picture, settings, and what you upload.

## 16. Data import pipeline — signed off 2026-10-27

**What it's for.** Keep all game data up to date automatically.

**Every day (MVP).** A background job early each morning: downloads the game files and PvPoke rankings; recalculates max CPs, raid rankings, counters, best IVs and search filters; checks results (every string tested by simulating the game's search — on failure the old data stays live and the problem shows in the admin panel); publishes to the database.

**Portuguese names (MVP).** Official Portuguese Pokémon, move and type names from the game's text files.

**Data freshness (MVP).** "Data as of" dates on pages (game data, rankings, raid bosses); a warning when an import is overdue.

**Later (Good to have).** Change history, including a backfill of years of past changes from the game files' public history (powers "changes over time"); preview big changes before publishing (small changes publish automatically).

**Future.** Public "What changed" page.

**Where it comes from.** Datamined game files (public on GitHub), PvPoke (MIT), datamined game text.

## 17. Offline and installing — signed off 2026-10-27

**What it's for.** Use the web app like a phone app, and later keep it useful without signal.

**Install on your phone (MVP).** "Add to Home Screen" (iPhone: Share → Add to Home Screen; Android: install prompt): own icon, full screen, no app store, automatic updates. In the MVP it still needs a connection; offline it shows a friendly "You're offline" screen.

**Later (Good to have): read-only offline.** A device copy of the Pokédex, search filters, raid counters and your collection, refreshed when online: look up Pokémon, copy filters, check counters, browse your collection without signal. Uploading, saving and the AI need a connection.

**Not planned.** Offline edits that sync later; choosing what's kept offline.

## 18. Team GO Rocket — signed off 2026-10-27 (all after the MVP)

**What it's for.** Beat Grunts, Leaders and Giovanni, and decide what to do with rescued Shadows.

**Getting there.** From the menu (Team GO Rocket).

**Line-ups and counters (Good to have).** Current line-ups fetched by the AI researcher (Grunts by type; Cliff, Arlo, Sierra with their three slots; Giovanni), best counters for each Pokémon, and which Shadows each can give.

**Your counters (Good to have).** Best counters from your own collection per Leader/Giovanni slot.

**Counter search string (Good to have).** A string that finds your counters for a Leader.

**Purify advice on your Shadows (Good to have).** Keep as Shadow or purify, per league and for raids, with the purification cost.

**Future.** Grunt by quote.

**Where it comes from.** AI researcher (line-ups; weekly, auto-published, fixable), our battle calculations, your collection.

## 19. Eggs & research — signed off 2026-10-27

**What it's for.** Know what can hatch and which research rewards are available, and spot a good one.

**Getting there.** From the menu (Eggs & Research) and the home page.

**Egg pools and research rewards (MVP).** Eggs per distance (2, 5, 7, 10, 12 km, Adventure Sync, route eggs) with the Pokémon that can hatch now; Field Research tasks with their rewards; hundo CPs at the level each comes at (L20 eggs, L15 research). From the AI researcher's weekly fetch.

**Later (Good to have).** Shiny and rarity markers; catch-screen IV check for research and hatches (10/10/10 floor; same tool as raids).

**Future.** Find a task by reward; manual research checklist.

**Where it comes from.** AI researcher (pools, tasks; weekly, auto-published, fixable), our calculations (CPs).

## 20. Trading & friends — signed off 2026-10-27 (all after the MVP)

**What it's for.** Know what a trade costs and can give, and which of your Pokémon are worth trading.

**Getting there.** From the menu (Trading).

**Trade rules and costs (Good to have).** By friendship level (Good, Great, Ultra, Best): Stardust costs (normal, new Pokédex entry, special trades), lucky-trade chances and guarantees, trade evolutions (0 candy after a trade), friendship bonuses (raid damage, extra balls, minimum IVs on trades).

**Trade IV calculator (Good to have).** Pick a Pokémon and friendship level → possible IV range (minimum IV: Good 1, Great 2, Ultra 3, Best 5, Lucky 12) and its chance of a good GL/UL/raid specimen.

**Future.** What to trade (regionals, likely-lucky old catches, trade-evolution candidates, duplicates — from your collection).

**Where it comes from.** Fixed game rules (kept in the app, updated by the admin), our calculations.
