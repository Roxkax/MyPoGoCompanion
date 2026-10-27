# MyPoGoCompanion — priorities

Stage 8. Proposed by Claude, **adjusted by the owner on 2026-10-27** (changed rows are marked). Pending the owner's sign-off per feature.

- **MVP** = the first release (web app + API) · **Good to have** = next, after the MVP · **Future** = no commitment.
- Foundation work (repo, CI/CD, hosting, database, sign-in, OpenAPI clients, the C# engine port) is implied by the MVP and is planned in the backlog.

**Totals:** 55 MVP · 75 Good to have · 28 Future (158 items).

## 1. Pokédex

| ID | Item | Proposed | Why |
|---|---|---|---|
| 1.1 | Browse all species and forms (one row per form) | **MVP** | Core of the app |
| 1.2 | Search by name, number or nickname | **MVP** | Core |
| 1.3 | Filters: type, generation, stats, Shadow/Mega/Dynamax, legendary, released | **MVP** | Core |
| 1.4 | Sort by any column | **MVP** | Core |
| 1.5 | Move filters (incl. Elite TM / legacy) | **Good to have** | Useful, not needed day one |
| 1.6 | Availability filters (shiny, in raids now) | **Good to have** | Needs the AI research data (13.2) |
| 1.7 | "I own it" markers and owned/missing filter | **Good to have** | Cheap once the collection exists; makes it personal; owner: changed 2026-10-27 |
| 1.8 | Collection tracker (completion by region and category) | **Good to have** | Collecting is a focus, but builds on 1.7 |
| 1.9 | Compare 2–4 Pokémon | **Good to have** |  |
| 1.10 | Saved views | **Future** | Nice convenience |

## 2. Pokémon page

| ID | Item | Proposed | Why |
|---|---|---|---|
| 2.1 | Stats, max CP L40/50/51, 100% CP cheat sheet | **MVP** | Core |
| 2.2 | Movepool with raid and PvP stats, Elite TM/legacy/event flags | **MVP** | Core |
| 2.3 | Evolution chain with every condition | **MVP** | Core; in the game files |
| 2.4 | How to get it: permanent ways | **MVP** | Core |
| 2.5 | How to get it: special conditions (curated, from AI + admin) | **Good to have** | Your main AI idea; owner: changed 2026-10-27 |
| 2.6 | How to get it: current appearances | **MVP** | Needs the AI research job (13.2); owner: changed 2026-10-27 at sign-off |
| 2.7 | Versions: forms, costumes, shiny, Shadow/Purified, Mega, Dynamax | **MVP** | Core |
| 2.8 | Changes over time (history backfill) | **Good to have** | Sizeable data work |
| 2.9 | Rankings summary (PvP, raids, Max) | **MVP** | Core |
| 2.10 | Best IVs per league + search string | **MVP** | Engine already exists |
| 2.12 | Report wrong data | **Good to have** | Feeds the review queue; owner: changed 2026-10-27 |
| 2.13 | Your copies (from your collection) | **Good to have** | Cheap once the collection exists; owner: changed 2026-10-27 |
| 2.14 | IV rank checker | **MVP** | Cheap with the engine |
| 2.15 | Type matchups | **MVP** | Cheap |
| 2.16 | Power-up cost calculator | **Good to have** |  |

## 3. Moves

| ID | Item | Proposed | Why |
|---|---|---|---|
| 3.1 | Moves list with raid and PvP stats, type filter | **MVP** | Pages link to moves |
| 3.2 | Who learns each move | **MVP** |  |
| 3.3 | Move history | **Good to have** | Comes with the history backfill (2.8) |
| 3.4 | Max and G-Max move power | **Good to have** | With Max Battles |
| 3.5 | Move rankings per type | **Good to have** |  |
| 3.6 | Type chart | **MVP** | Cheap reference |
| 3.7 | TM advice on your Pokémon | **Good to have** |  |
| 3.8 | Moveset ranking per Pokémon | **MVP** | The raid engine already scores every moveset |

## 4. Search filters

| ID | Item | Proposed | Why |
|---|---|---|---|
| 4.1 | Keeper filters: GL, UL, raids, Max, Megas (rebuilt automatically) | **MVP** | Port of today's page |
| 4.2 | Safe to transfer (inverse) | **MVP** | Cheap with the engine |
| 4.3 | Family strings | **MVP** | Engine already exists |
| 4.4 | Filter builder with live string | **MVP** | Part of your idea |
| 4.5 | Builder options: keeper %, level cap 50/51 | **Good to have** |  |
| 4.6 | Explain a string | **Good to have** |  |
| 4.7 | Preview matches on your collection | **Good to have** |  |
| 4.8 | Save and name filters | **MVP** |  |
| 4.9 | Utility filter library | **Good to have** |  |
| 4.10 | Cleanup routine | **Future** |  |

## 5. Tier lists

| ID | Item | Proposed | Why |
|---|---|---|---|
| 5.1 | PvP: Great, Ultra, Master League (PvPoke) in tiers | **MVP** | Part of your idea |
| 5.2 | PvP: cups (incl. Little Cup) | **Future** | owner: changed 2026-10-27 |
| 5.3 | Raids: per type and overall, with the numbers | **MVP** | Engine already exists |
| 5.4 | Max Battles: attackers, tanks, supports (estimate) | **Good to have** | New model |
| 5.5 | Only what I own | **MVP** | Cheap once the collection exists |
| 5.6 | Your gaps & what to build | **Good to have** |  |
| 5.7 | Budget picks | **Future** |  |
| 5.8 | Risers & fallers | **Future** | Needs ranking snapshots over time |
| 5.9 | Why it's ranked here (PvP and Max) | **Good to have** | Raids already have it in 5.3 |

## 6. Raid bosses

| ID | Item | Proposed | Why |
|---|---|---|---|
| 6.1 | Current and upcoming bosses by tier (fetched by the AI researcher) | **MVP** | Part of your idea |
| 6.2 | Boss detail: types, weaknesses, moves, 100% CP | **MVP** |  |
| 6.3 | Best counters, re-ranked by boss moveset | **MVP** | Part of your idea |
| 6.4 | Your best counters | **MVP** |  |
| 6.5 | Tips | **Good to have** | Admin-written content |
| 6.6 | Estimated minimum players | **Good to have** | Needs the simulator (8.1–8.2) |
| 6.7 | Counter search string | **Good to have** | Cheap with the engine; owner: changed 2026-10-27 |
| 6.8 | Can I do it with my team? | **Good to have** | Needs the simulator |
| 6.9 | Catch-screen IV check | **MVP** | Small CP→IV solver, reused in 7 and 19 |
| 6.10 | Weather check (approximate) | **Future** |  |

## 7. Max Battles

| ID | Item | Proposed | Why |
|---|---|---|---|
| 7.1 | Current Max bosses with counters | **Good to have** | Needs the Max model (5.4) |
| 7.2 | Your best Dynamax/Gigantamax for each | **Good to have** |  |
| 7.3 | Tips | **Good to have** |  |
| 7.4 | Team roles (attacker, tank, healer) | **Good to have** |  |
| 7.5 | Max Move upgrade cost | **Future** |  |
| 7.6 | Catch-screen IV check | **Good to have** | Reuses 6.9 |

## 8. Battle simulator

| ID | Item | Proposed | Why |
|---|---|---|---|
| 8.1 | Raid simulator: time to win, faints | **Good to have** | Large; counters cover most needs first |
| 8.2 | Group raids → minimum players | **Good to have** | Powers 6.6 and 6.8 |
| 8.3 | Max Battle simulator | **Future** | Least documented mechanics |
| 8.4 | Battle timeline | **Future** |  |
| 8.5 | Save simulations | **Future** |  |

## 9. My collection

| ID | Item | Proposed | Why |
|---|---|---|---|
| 9.1 | Poke Genie upload, name/form matching, unmatched report | **MVP** | Part of your idea |
| 9.2 | Collection list: search and sort | **MVP** |  |
| 9.3 | PvP rank per league for each Pokémon | **MVP** |  |
| 9.4 | Raid value for each Pokémon | **MVP** |  |
| 9.5 | Re-upload replaces and shows what changed | **MVP** |  |
| 9.6 | Suggestions: power up, safe to transfer, missing | **Good to have** |  |
| 9.7 | Collection stats | **Good to have** |  |
| 9.8 | Duplicates review | **Good to have** |  |
| 9.9 | Import history | **Future** |  |

## 10. Best team

| ID | Item | Proposed | Why |
|---|---|---|---|
| 10.1 | Best 6 per attacking type (one Mega slot) | **MVP** | Part of your idea |
| 10.2 | Best team against a boss | **MVP** | Comes from counters (6.3) |
| 10.3 | Best team from your collection | **MVP** |  |
| 10.4 | Mega choice (lock your Mega) | **MVP** | Today's "Your best Mega" selector |
| 10.5 | Save teams | **Good to have** |  |
| 10.6 | PvP team builder | **Future** |  |

## 11. AI assistant

| ID | Item | Proposed | Why |
|---|---|---|---|
| 11.1 | Chat that answers from our data (tools) | **MVP** | Part of your idea |
| 11.2 | Web search with sources for gaps | **Good to have** | Needed for the correction loop; owner: changed 2026-10-27 |
| 11.3 | Saved conversations | **Good to have** | owner: changed 2026-10-27 |
| 11.4 | $5 cap and small-model routing | **MVP** | Cost control from day one |
| 11.5 | Knows your collection | **Good to have** |  |
| 11.6 | Knows the current page | **Good to have** |  |
| 11.7 | "Ask about this" buttons | **Good to have** |  |
| 11.8 | Does things for you (after confirming) | **Future** |  |
| 11.9 | Screenshot questions | **Future** | Costs more per question |

## 12. Data corrections

| ID | Item | Proposed | Why |
|---|---|---|---|
| 12.1 | Proposal queue from chat and reports: approve / edit / reject | **Good to have** | Part of your idea; owner: changed 2026-10-27 |
| 12.2 | Curated layer + admin-verified badge | **Good to have** | Part of your idea; owner: changed 2026-10-27 |
| 12.3 | Conflict detection on later imports | **Good to have** | Keeps approved facts correct; owner: changed 2026-10-27 |
| 12.4 | Required sources + AI confidence | **Good to have** | Cheap; protects data quality; owner: changed 2026-10-27 |
| 12.5 | Field history & revert | **Good to have** |  |
| 12.6 | Pending alerts (in-app) | **Good to have** |  |

## 13. Game calendar

| ID | Item | Proposed | Why |
|---|---|---|---|
| 13.1 | Current and upcoming rotations and events (fetched by the AI researcher) | **MVP** | Needed by 6.1 and the home page |
| 13.2 | Weekly AI research job: fetches rotations, events and pools (sources such as LeekDuck) and publishes them automatically | **MVP** | owner: changed 2026-10-27 — the system fetches everything; the admin only fixes |
| 13.3 | Home page "Today in the game" | **MVP** |  |
| 13.4 | Past events archive | **Good to have** |  |
| 13.5 | Event prep checklist | **Future** |  |
| 13.6 | Calendar sync (iCal) | **Future** |  |
| 13.7 | Reminders (in-app) | **Future** |  |
| 13.8 | Event details: your copies of the featured Pokémon with evolve / keep / transfer advice | **MVP** | owner: added 2026-10-27 at design review (A3) |

## 14. Admin panel

| ID | Item | Proposed | Why |
|---|---|---|---|
| 14.1 | Import status and re-run | **MVP** |  |
| 14.2 | Review queue (12) | **Good to have** | owner: changed 2026-10-27 |
| 14.3 | Direct data edits (logged) | **Good to have** | owner: changed 2026-10-27 |
| 14.4 | Hand-maintained lists | **Good to have** | owner: changed 2026-10-27 |
| 14.5 | AI usage and cap setting | **Good to have** | owner: changed 2026-10-27 |
| 14.6 | Audit log | **Good to have** | owner: changed 2026-10-27 |
| 14.7 | Users and roles | **Good to have** | Mainly you at launch |
| 14.8 | Content editor (EN/PT) | **Good to have** |  |
| 14.9 | Health & limits | **Good to have** |  |
| 14.10 | Backups (downloadable) | **Good to have** | Neon's restore window covers the MVP |
| 14.11 | Feature switches | **Good to have** | owner: changed 2026-10-27 |
| 14.12 | Fix or remove a fetched boss or event (small editor) | **MVP** | owner: added 2026-10-27 — small fixes to what the AI researcher publishes |

## 15. Account and settings

| ID | Item | Proposed | Why |
|---|---|---|---|
| 15.1 | Google sign-in and roles | **MVP** |  |
| 15.2 | Language English / Português | **MVP** | Decided for launch |
| 15.3 | Theme light/dark | **MVP** |  |
| 15.4 | Calculation defaults | **Good to have** |  |
| 15.5 | Preferred league | **Good to have** |  |
| 15.6 | Trainer profile | **Good to have** |  |
| 15.7 | Delete account & export data | **Good to have** | Must exist before any public launch |
| 15.8 | Notification settings | **Future** | With notifications |
| 15.9 | Sign in with Apple | **Good to have** | With the iOS app |

## 16. Data import pipeline

| ID | Item | Proposed | Why |
|---|---|---|---|
| 16.1 | Daily game master + PvPoke import, recalculation, verification | **MVP** |  |
| 16.2 | Portuguese names from game text | **MVP** | Needed for the Portuguese interface |
| 16.4 | Change detection & history | **Good to have** | With 2.8 |
| 16.5 | Preview big changes before publishing | **Good to have** |  |
| 16.6 | "What changed" page | **Future** |  |

## 17. Offline

| ID | Item | Proposed | Why |
|---|---|---|---|
| 17.1 | Installable web app | **MVP** | Cheap with a Vite PWA |
| 17.2 | Read-only offline cache | **Good to have** |  |

## 18. Team GO Rocket

| ID | Item | Proposed | Why |
|---|---|---|---|
| 18.1 | Line-ups with counters per slot, Shadows given | **Good to have** |  |
| 18.2 | Your counters | **Good to have** |  |
| 18.3 | Counter search string | **Good to have** |  |
| 18.4 | Purify advice on your Shadows | **Good to have** |  |
| 18.5 | Grunt by quote | **Future** |  |

## 19. Eggs & research

| ID | Item | Proposed | Why |
|---|---|---|---|
| 19.1 | Egg pools and research rewards with hundo CPs | **MVP** | owner: changed 2026-10-27 at sign-off — the researcher already fetches them |
| 19.2 | Shiny & rarity markers | **Good to have** |  |
| 19.3 | Catch-screen IV check | **Good to have** | Reuses 6.9 |
| 19.4 | Find a task by reward | **Future** |  |
| 19.5 | Research checklist | **Future** |  |

## 20. Trading & friends

| ID | Item | Proposed | Why |
|---|---|---|---|
| 20.1 | Trade rules, costs, friendship bonuses | **Good to have** |  |
| 20.2 | Trade IV calculator | **Good to have** |  |
| 20.3 | What to trade (from your collection) | **Future** |  |

## 21. Routes & Party Play

| ID | Item | Proposed | Why |
|---|---|---|---|
| 21.1 | Rules, bonuses and current challenges | **Future** |  |
| 21.2 | Route rewards | **Future** |  |
| 21.3 | Zygarde Cell tracker | **Future** |  |
| 21.4 | Party Power planner | **Future** |  |

## 22. Mobile apps

| ID | Item | Proposed | Why |
|---|---|---|---|
| 22.1 | iOS app (SwiftUI) | **Good to have** | First after the web MVP |
| 22.2 | Android app (Kotlin) | **Future** | After iOS |
