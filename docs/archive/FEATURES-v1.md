# MyPoGoCompanion — feature decisions

Decided with the owner feature by feature (Oct 2026). Priorities:
**MVP** = first release (web app + API) · **Must-have** = required, after the MVP · **Future** = no commitment.
Where the owner gave no preference, the proposed default was kept and is marked *(default)*.

## Platform decisions

- API: .NET 10 (ASP.NET Core, EF Core, PostgreSQL, OpenAPI). The Python engine is ported to C# and checked against golden files from the Python build.
- Web: React + TypeScript. Mobile later: native SwiftUI (iOS, first) and Kotlin/Jetpack Compose (Android).
- Sign-in: Google OAuth (Sign in with Apple added with the iOS app).
- Only the admin approves data changes: admin panel with review queue and audit log.
- Free to use; hosting on free tiers. The AI chat is the only running cost.
- Name: MyPoGoCompanion. Unofficial fan project; no Pokémon/Niantic logos. Game sprites are used for images.

## Summary

| ID | Feature | First release |
|---|---|---|
| F01 | Accounts & Google sign-in | MVP |
| F02 | Data import pipeline | MVP |
| F03 | Admin panel | MVP |
| F04 | Web platform & hosting | MVP |
| F05 | Pokédex browse & filters | MVP |
| F06 | Pokémon detail page | MVP |
| F07 | Moves database | MVP |
| F08 | Prebuilt search filters | MVP |
| F09 | Custom filter builder | MVP |
| F10 | PvP tier lists | MVP |
| F11 | Raid tier lists | MVP |
| F12 | Max Battle tier lists | MVP |
| F13 | Raid bosses & schedule | MVP |
| F14 | Raid counters, tips & min players | MVP |
| F15 | Best team by type | MVP |
| F16 | Poke Genie import | MVP |
| F17 | My collection | MVP |
| F18 | IV rank checker | Future |
| F19 | PvP battle simulator | Must-have |
| F20 | Raid battle simulator | MVP |
| F21 | AI assistant chat | Must-have |
| F22 | AI corrections & curated data | Must-have (the review queue for user reports is MVP) |
| F23 | Events calendar | MVP |
| F24 | Localisation | Must-have |
| F25 | Native mobile apps | Must-have (iOS first) |

## F01 Accounts & Google sign-in

| Item | Priority |
|---|---|
| Google sign-in (API checks the Google token, issues its own session token) | MVP |
| Roles: User, Admin (first admin = owner's email in config) | MVP |
| Browse without sign-in (sign-in needed for collection, saved filters/teams, AI) | MVP |
| Profile & settings sync (name, preferred league, language, theme) | MVP |
| Delete account & export data (GDPR) | MVP |
| Sign in with Apple | Must-have (with the iOS app) |

## F02 Data import pipeline

| Item | Priority |
|---|---|
| Daily game master import (species, forms, moves, evolution requirements, Mega/Dynamax) | MVP |
| PvPoke import (rankings GL/UL/ML/cups, Elite TM & legacy flags) | MVP |
| Community data import (raid bosses, events, eggs, research, shiny) — check terms of LeekDuck/ScrapedDuck, pogoapi.net | MVP |
| Recompute after import, verified by the search simulator | MVP |
| History backfill from the game master's git history (needed by F06) | MVP |
| Public "What changed" changelog page | Must-have |

## F03 Admin panel

| Item | Priority |
|---|---|
| Admin-only area (/admin) | MVP |
| Import status (runs, errors, data dates, re-run) | MVP |
| Hand-maintained lists (Megas not in GO yet, raid rotations, events, boss tips) | MVP |
| Direct data edits (curated layer, no approval step, logged) | MVP |
| Review queue for user reports (needed by F06 "Report wrong data") | MVP |
| Audit log (who changed what, with undo) | MVP |
| Users & roles (promote admins, block) | MVP |
| AI usage & cost (messages/cost per day, daily limit setting) | MVP (shows data once F21 ships) |
| AI proposal review (same queue) | Must-have (with F21/F22) |

## F04 Web platform & hosting

| Item | Priority |
|---|---|
| React + TypeScript web app, works on phone browsers | MVP |
| .NET API with OpenAPI and generated clients | MVP |
| Free hosting & CI/CD (GitHub Actions) | MVP |
| Legal pages (unofficial disclaimer, privacy policy, terms) | MVP |
| Pre-rendered Pokédex pages (search-engine indexing) | MVP |
| Installable & offline (filters, Pokédex, collection) | MVP |
| Error monitoring & analytics (privacy-friendly) | MVP |

## F05 Pokédex browse & filters

| Item | Priority |
|---|---|
| List of all Pokémon (species + forms) | MVP |
| Name / number / nickname search | MVP |
| Type, generation and flag filters | MVP |
| Stat filters & sorting | MVP |
| Move filters (incl. Elite TM / legacy) | MVP |
| Availability filters (shiny, released, current raids/eggs/wild) | MVP |
| Owned overlay (from collection) | Must-have |
| Compare 2–4 Pokémon | Must-have |

Display: one row per form (regional forms, Megas, Shadows each their own row). Default row columns: image, name, number, types, base stats, max CP, PvP ranks (GL/UL/ML), best raid score.

## F06 Pokémon detail page

All MVP: stats, forms & versions, movepool (Elite TM / legacy / event flags), evolution requirements, how to get it, rankings summary, best IVs with search string, history of changes, source badges, report wrong data, costume & shiny gallery.

- "Versions" covers: forms & costumes (incl. retired), variants (shiny, Shadow, Purified, Mega, Dynamax, Gigantamax), changes over time (movepool/stat rebalances), event appearances (from F13/F23 archives).
- How to get it: permanent ways + current appearances (community data).
- CP info: max CP at L40/50/51 and the 100% CP cheat sheet (L15/20/25/30/35). No CP ranges or CP calculator.

## F07 Moves database

| Item | Priority |
|---|---|
| Moves list (raid + PvP stats, type filter) | MVP |
| Move detail (who learns it) | MVP |
| Max Moves (Max / G-Max power by level) | MVP |
| Move rebalance history | Must-have |

## F08 Prebuilt search filters

| Item | Priority |
|---|---|
| Today's 5 tabs (GL, UL, raids, Max, Megas), rebuilt daily | MVP |
| Copy & tag guidance | MVP |
| Family strings | MVP |
| Transfer ("trash") strings | MVP |
| Strings in other game languages — first Portuguese (keywords are the game's pt-BR text; UI can be pt-PT) | Must-have |
| Master League filter | Must-have |
| Cup filters (incl. Little Cup) | Must-have |

## F09 Custom filter builder

| Item | Priority |
|---|---|
| Builder: form + live string, editable as text | MVP |
| Validation (simulator, 1,950-char split) | MVP |
| Preview on your collection | MVP |
| Save filters (account) | MVP |
| Share by link | MVP |
| Generated-filter option: Keeper % (95/98/99) | MVP |
| "Explain a string" (plain-language explanation + matches) | MVP |
| Options: level cap 50/51, Master League & cups, Megas you own | Must-have |
| Community filter library | Future |

## F10 PvP tier lists

Source: PvPoke rankings.

| Item | Priority |
|---|---|
| Great, Ultra and Master League lists | MVP |
| Tier groups (S/A/B/C) | MVP *(default)* |
| Cups (incl. Little Cup) | Must-have *(default)* |
| List filters (type, Shadow) | Must-have *(default)* |
| Owned only | Must-have *(default)* |

## F11 Raid tier lists

| Item | Priority |
|---|---|
| Ranking per type, numbers table, "Your best Mega" slot | MVP |
| Overall list (best per type, generalists) | MVP |
| Settings (level 40/50/51, Shadows/Megas, weather) | Must-have |
| Owned only | Must-have |

## F12 Max Battle tier lists

| Item | Priority |
|---|---|
| Max attackers by type | MVP |
| Tanks & support (Max Guard / Max Spirit) | MVP |
| G-Max moves | MVP |
| Max Battle bosses with counters | MVP |

## F13 Raid bosses & schedule

All MVP: current bosses by tier (1, 3, 5, Mega, Shadow, Elite) with dates and shiny, upcoming bosses, boss detail page (types, weaknesses, movesets, 100% CP normal/weather, weather boost), past bosses archive.

## F14 Raid counters, tips & min players

| Item | Priority |
|---|---|
| Best counters per boss — averaged over boss movesets, re-ranked when you pick the boss's moves | MVP |
| Minimum players (from the F20 group simulation, labelled estimate) | MVP |
| Tips per boss (admin-written; AI can propose later) | MVP |
| Your best counters (from collection) | MVP |
| Counter search string | Must-have |

## F15 Best team by type

All MVP: best 6 per attacking type (one Mega slot), best team vs a boss, best team from your collection, PvP team builder (3 Pokémon, type coverage), saved teams.

## F16 Poke Genie import

| Item | Priority |
|---|---|
| CSV upload; names, forms, IVs, levels, moves, Shadow/Purified, lucky, favourite | MVP |
| Name & form matching, report of rows that didn't match | MVP |
| Stored in the account, synced across devices (sign-in required) | MVP |
| Re-import replaces the previous one and shows what's new, gone or changed | MVP |
| Other apps' exports (Calcy IV…) | Future |

## F17 My collection

All MVP: searchable/sortable list, PvP value (IV rank GL/UL/ML), raid value, suggestions (power up, safe to transfer, missing), collection stats (types, shinies, luckies, hundos, dex completion).

## F18 IV rank checker — Future

Enter a Pokémon and IVs → rank, best level and CP per league, plus evolutions.

## F19 PvP battle simulator — Must-have

1v1 with movesets, IVs and shields, built on PvPoke's MIT battle engine. Extras — matchup matrix, 3v3 team battles, use your collection — are Future *(default)*.

## F20 Raid battle simulator — MVP

| Item | Priority |
|---|---|
| One trainer, 6 attackers vs a boss: boss moveset, energy, re-lobbies, time to win | MVP |
| Group raids (N trainers → minimum players) | MVP *(default)* |
| Weather & friendship bonuses | MVP *(default)* |
| Party Power | Must-have *(default)* |
| Dodging | Future *(default)* |

## F21 AI assistant chat — Must-have

| Item | Priority |
|---|---|
| Answers from our own data (tools over Pokédex, rankings, bosses, simulations) | Must-have |
| Web search with cited sources | Must-have |
| Knows your collection | Must-have |
| Chat history in the account | Must-have |
| Knows the current page | Future |
| Daily message limit per user, set by the admin (start ~20/day) | Must-have |

## F22 AI corrections & curated data — Must-have

Proposal sources: AI chat findings, user reports (MVP via F03), weekly scheduled AI research (new Megas, raid rotations, events, debuts). Admin direct edits skip the queue but are logged.

Flow: proposal (entity, field, current value, proposed value, sources, link to chat) → admin review (approve / edit + approve / reject with reason) → curated layer (survives daily imports, revertible) → Pokédex shows it with an "admin-verified" badge → a later import that changes the same field is flagged as a conflict.

## F23 Events calendar

| Item | Priority |
|---|---|
| Calendar of current and upcoming events | MVP *(default — needed by F06/F13)* |
| Event detail (featured Pokémon, bonuses, exclusive moves, research) | MVP *(default)* |
| Past events archive | MVP *(default — needed by F06 event appearances)* |
| Reminders (email / push) | Must-have *(default)* |

## F24 Localisation — Must-have

MVP is English only *(default)*. Then Portuguese: UI (pt-PT), search strings (game pt-BR keywords), Pokémon and move names.

## F25 Native mobile apps — Must-have

iOS first (SwiftUI, Sign in with Apple, Apple developer account), then Android (Kotlin, Jetpack Compose). Both use the same API through generated clients.
