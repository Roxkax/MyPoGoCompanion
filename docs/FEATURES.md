# MyPoGoCompanion — feature decisions

Decided with the owner feature by feature (Oct 2026). Priorities:
**MVP** = first release (web app + API) · **Must-have** = required, after the MVP · **Future** = no commitment.

## Platform decisions

- API: .NET 10 (ASP.NET Core, EF Core, PostgreSQL, OpenAPI). The Python engine is ported to C# and checked against golden files from the Python build.
- Web: React + TypeScript. Mobile later: native SwiftUI (iOS) and Kotlin/Jetpack Compose (Android).
- Sign-in: Google OAuth (Sign in with Apple added with the iOS app).
- Only the admin approves data changes: admin panel with review queue and audit log.
- Free to use; hosting on free tiers. The AI chat is the only running cost.
- Name: MyPoGoCompanion. Unofficial fan project; no Pokémon/Niantic logos.

## Foundation (proposed, to be reviewed)

| ID | Feature | Status |
|---|---|---|
| F01 | Accounts & Google sign-in | MVP: Google sign-in, roles, browsing without sign-in. Saved filters (F09) need sign-in in the MVP |
| F02 | Data import pipeline | MVP: game master, PvPoke, community data, recompute after import. **History backfill pulled into MVP** by F06 |
| F03 | Admin panel | MVP: admin area, import status, hand-maintained lists. **Review queue for "Report wrong data" pulled into MVP** by F06 |
| F04 | Web platform & hosting | MVP: React app, .NET API, free hosting & CI/CD, legal pages |

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

Display: one row per form (regional forms, Megas, Shadows each their own row). Game sprites for images. Default row columns: name, number, types, base stats, max CP, PvP ranks (GL/UL/ML), best raid score.

## F06 Pokémon detail page

All MVP: stats, forms & versions, movepool (Elite TM / legacy / event flags), evolution requirements, how to get it, rankings summary, best IVs with search string, **history of changes**, **source badges**, **report wrong data**, **costume & shiny gallery**.

- "Versions" covers: forms & costumes (incl. retired), variants (shiny, Shadow, Purified, Mega, Dynamax, Gigantamax), changes over time (movepool/stat rebalances), event appearances.
- How to get it: permanent ways + current appearances (community data). Full past-appearance history needs the event archive (F23).
- CP info: max CP at L40/50/51 and the 100% CP cheat sheet (L15/20/25/30/35). CP ranges and a CP calculator are not planned.

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
| Strings in other game languages | Must-have — first: Portuguese (keywords are the game's pt-BR text; UI can be pt-PT) |
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

| Item | Priority |
|---|---|
| Great, Ultra and Master League lists (PvPoke rankings) | MVP |
| Tier groups (S/A/B/C) | MVP |
| Cups (incl. Little Cup) | Must-have |
| List filters (type, Shadow) | Must-have |
| Owned only | Must-have |

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

## Still to decide

F13 Raid bosses & schedule · F14 Raid counters, tips & min players · F15 Best team by type · F16 Poke Genie import · F17 My collection · F18 IV rank checker · F19 PvP simulator · F20 Raid simulator · F21 AI chat · F22 AI corrections & curated data · F23 Events calendar · F24 Localisation · F25 Native mobile apps · then a review of F01–F04.
