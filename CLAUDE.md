# MyPoGoCompanion — project context

A free companion system for Pokémon GO players: a shared API, a web app (first) and native mobile apps (later).
The repository today holds a working Python pipeline that builds copy-paste storage search strings ("Keeper Filters"); its full context is in `docs/KEEPER-FILTERS.md`. Read it before touching `scripts/`, `web/` or `build.py`.

We are **planning**, following the process in `docs/PLANNING-PROCESS.md` (stages 1–12). **Don't start application code until the owner says to start a sprint.**

## Working agreements

- **Ask one question at a time, as plain text in the chat** so the owner can answer freely (no multiple-choice pop-ups). Options and a recommendation can be listed in the question.
- **The owner describes features from the user's point of view; Claude works out the backend and confirms it** before treating it as decided.
- **Be honest about feasibility.** Say what the data or the game allows and offer realistic alternatives (e.g. there is no official game API, so the collection comes from Poke Genie exports).
- **Scope rule:** the product is free. Every feature is in scope; anything "premium" is only noted for a possible later commercial phase.
- **Cost rule:** prefer free tiers, pay-per-use and scale-to-zero services. Flag anything with a running cost (the AI).
- **Commercial readiness:** design for many users from day one, and flag licensing, terms-of-service and trademark issues (Pokémon/Niantic data, sprites, community sources).
- **Language:** replies in English. The owner may write in Portuguese.
- **Git:** commit and push planning documents after every confirmed change. Commits are authored as `Diogo Gonçalves <3799595+Roxkax@users.noreply.github.com>` (set `git config user.name/user.email` in a new clone); commits Claude writes keep the `Co-Authored-By: Claude` trailer.
- **Decision log:** every confirmed decision goes in the log below, with its date, immediately.

## Planning status

| Stage | Status |
|---|---|
| 1 Kick-off questions | Done (platforms and purpose known from earlier sessions) |
| 2 Working agreements | Done 2026-10-27 |
| 3 Describe the idea as a user | Done 2026-10-27 (see "The idea") |
| 4 Clarify, one question at a time | Done 2026-10-27 |
| 5 Write PLAN.md | Done 2026-10-27 (draft) |
| 6 Feature-by-feature review | Done 2026-10-27 (21 features) |
| 7 Technical decisions | Done 2026-10-27 |
| 8 Prioritise (MVP / Good to have / Future) | Done 2026-10-27 (`planning/PRIORITIES.md`; descriptions signed off in `planning/SPECS.md`) |
| 9 Backlog (Epic → Feature → Task, 100-hour sprints) | Moved after the design (built in stage 11) |
| 10 Design: app map, then realistic screens one by one | **Next** |
| 11 Rebuild the backlog from the design | — |
| 12 Start sprints | Only when the owner says so |

Earlier product work (feature list F01–F25 with priorities, UI choices, first wireframes) is archived in `docs/archive/` for reference only. It is **not** decided: the product is being redone with this process.

## The idea (owner's words, 2026-10-26)

- An extensive Pokédex: each Pokémon's stats, full movepool, where to get it and any special conditions, every version it has or had; filter by Attack, type and more.
- Create search filters.
- Tier lists for PvP, raids and Dynamax.
- Raid boss list with counters, tips and minimum players.
- A battle simulator.
- Import my Poke Genie export so my collection is in the app.
- Suggestions for the best team per type.
- An AI chat to answer doubts. When it finds something the Pokédex is missing (e.g. a special catch condition) and the admin approves it, the Pokédex is updated.

## Decision log

| Date | Decision |
|---|---|
| 2026-10-26 | Product name: **MyPoGoCompanion**. Unofficial fan project; no Pokémon/Niantic logos or trademarks in branding. GitHub repo to be renamed to `MyPoGoCompanion` by the owner. |
| 2026-10-26 | Free for users. Hosting on free tiers; the AI is the only expected running cost. |
| 2026-10-26 | Architecture: one shared API used by a web app and mobile apps. **MVP = web app + API**; mobile apps come later. |
| 2026-10-26 | API in **.NET** (ASP.NET Core; .NET 10 LTS proposed), EF Core, PostgreSQL, OpenAPI with generated clients. The Python Keeper Filters engine is ported to C# and checked against golden files produced by the Python build. |
| 2026-10-26 | Web app in **React + TypeScript**. |
| 2026-10-26 | Mobile apps **native**: SwiftUI (iOS, first) and Kotlin/Jetpack Compose (Android). Sign in with Apple added with the iOS app (App Store rule 4.8). |
| 2026-10-26 | Sign-in with **Google OAuth** for now. |
| 2026-10-26 | **Only the admin approves data changes**, so the system needs an admin panel and approval logic. |
| 2026-10-27 | Adopted the planning process in `docs/PLANNING-PROCESS.md`. |
| 2026-10-27 | Keep the technical decisions above; **redo the product** (features, priorities, design) from scratch. v1 feature list, UX choices and wireframes archived. |
| 2026-10-27 | Working agreements above confirmed. |
| 2026-10-27 | Stage 3: the description above is the starting idea; more features can be added during the review. |
| 2026-10-27 | Audience at launch: **mainly the owner** (a personal tool; going public is a maybe for later). Still designed for many users. |
| 2026-10-27 | The owner plays raids, PvP (GO Battle League), Max Battles and collecting: all four get equal weight. |
| 2026-10-27 | App languages at launch: **English + Portuguese (pt-PT)** for the interface. Pokémon and move names in Portuguese come from the game (pt-BR text). |
| 2026-10-27 | The owner's game is set to **English**, so search strings use English keywords; strings in other game languages are not needed at launch. |
| 2026-10-27 | Offline: **read-only** — Pokédex, filters, raid counters and the user's collection are cached on the device; changes and the AI need a connection. |
| 2026-10-27 | Collection input: **Poke Genie CSV export only** (no official game API; reading the game breaks Niantic's terms). Re-upload to refresh. |
| 2026-10-27 | Battle simulator covers **raid battles and Max Battles**; no PvP simulator. Max Battle results are estimates (mechanics less documented). |
| 2026-10-27 | AI chat: **Claude via Anthropic's API**, pay-per-use, behind a provider-agnostic layer in the .NET API. |
| 2026-10-27 | Live game data (current raid bosses, Max Battles, events): a **weekly scheduled AI research job** proposes updates and the **admin approves** them; the admin can also edit by hand. No scraping of community sites. |
| 2026-10-27 | Images: **datamined game sprites**. Fine for personal use; recheck the trademark risk before any public launch. |
| 2026-10-27 | Hosting: **Azure Container Apps** (API + worker, scale-to-zero), **Azure Static Web Apps** (web, free), **Neon** (PostgreSQL, free tier). |
| 2026-10-27 | PvP tier lists come from **PvPoke rankings** (MIT), imported daily, for Great, Ultra, Master League and cups. |
| 2026-10-27 | AI spending cap: **$5/month for now** (adjustable in the admin panel); AI calls stop when it's reached and the admin is told. |
| 2026-10-27 | Build model: **Claude Code writes the code in sprints; the owner reviews, tests and approves.** Estimates include the owner's testing time. |
| 2026-10-27 | Owner availability: **10–20 hours per week** for reviewing and testing. |
| 2026-10-27 | Review — **1. Pokédex** confirmed, adding: "I own it" markers, collection tracker (completion by region and category), compare 2–4 Pokémon, saved views. |
| 2026-10-27 | Review — **2. Pokémon page** confirmed, adding: your copies, IV rank checker, type matchups, power-up cost calculator. |
| 2026-10-27 | Review — **3. Moves** confirmed, adding: move rankings per type, type chart, TM advice on the user's Pokémon, moveset ranking per Pokémon. |
| 2026-10-27 | Review — **4. Search filters** confirmed, adding: utility filter library (admin-curated), cleanup routine. Not added: Master League/cup keeper filters, "Megas you own". |
| 2026-10-27 | Review — **5. Tier lists** confirmed, adding: your gaps & what to build, budget picks, risers & fallers, "why it's ranked here" for every list. |
| 2026-10-27 | Review — **6. Raid bosses** confirmed, adding: counter search string, "can I do it with my team", catch-screen IV check, approximate weather check (free weather service, location not stored). |
| 2026-10-27 | Review — **7. Max Battles** confirmed, adding: team roles (attacker/tank/healer), Max Move upgrade cost, catch-screen IV check. Not added: Max counter search string. |
| 2026-10-27 | Review — **8. Battle simulator** confirmed, adding: battle timeline, saved simulations. Not added: compare teams, Party Power and dodging. |
| 2026-10-27 | Review — **9. My collection** confirmed, adding: import history (every upload kept, stored as diffs), duplicates review. Not added: candy planner, notes and own tags. |
| 2026-10-27 | Review — **10. Best team** confirmed, adding: PvP team builder (coverage/threat check from PvPoke data), Mega choice. Not added: team search string, generalist team. |
| 2026-10-27 | Review — **11. AI assistant** confirmed, adding: knows the current page, does things for you after confirmation (filters, teams, simulations), screenshot questions, "ask about this" buttons. |
| 2026-10-27 | Review — **12. Data corrections** confirmed, adding: required sources + AI confidence, field history & revert, pending-proposal alerts (email now, push with mobile). Not added: bulk approve. |
| 2026-10-27 | Review — **13. Game calendar** confirmed, adding: past events archive, event prep checklist (AI-drafted, admin-approved), calendar sync (iCal link), reminders (email, later push). |
| 2026-10-27 | Review — **14. Admin panel** confirmed, adding: bilingual content editor, health & limits, feature switches, scheduled downloadable backups (Azure Blob, a few cents/month). |
| 2026-10-27 | Review — **15. Account and settings** confirmed, adding: trainer profile (level, team, friend code), calculation defaults, notification settings. Not added: devices & sessions. |
| 2026-10-27 | Review — **16. Data import pipeline** confirmed, adding: Portuguese names from datamined game text, public "what changed" page, data freshness dates/warnings, admin preview of big changes before they go live. |
| 2026-10-27 | Review — **17. Offline** confirmed, adding: installable web app. Not added: offline status indicator, choosing what's saved. |
| 2026-10-27 | Review — **new features added**: 18. Team GO Rocket, 19. Eggs & research, 20. Trading & friends, 21. Routes & Party Play (to be reviewed one by one). |
| 2026-10-27 | Review — **18. Team GO Rocket** confirmed, adding: your counters, counter search string, purify advice on your Shadows, Grunt lookup by quote. |
| 2026-10-27 | Review — **19. Eggs & research** confirmed, adding: shiny & rarity markers, find a task by reward, catch-screen IV check, manual research checklist. |
| 2026-10-27 | Review — **20. Trading & friends** confirmed, adding: trade IV calculator, "what to trade" from the collection. Not added: friends tracker. |
| 2026-10-27 | Review — **21. Routes & Party Play** confirmed, adding: Zygarde Cell tracker (manual), route rewards, Party Power planner. **Stage 6 done: 21 features confirmed.** |
| 2026-10-27 | Tech — web app is a **Vite single-page React app, installable (PWA)** with an offline cache. |
| 2026-10-27 | Tech — scheduled work (daily import, weekly AI research, backups, reminders) runs as **Azure Container Apps Jobs** (scale-to-zero, separate from the API). |
| 2026-10-27 | **No emails for now.** Alerts and reminders are **in-app notifications** (bell + browser notifications); push notifications come with the mobile apps. Features 12, 13 and 15 updated. |
| 2026-10-27 | Tech — AI web search via a **separate search API** behind a pluggable interface; provider chosen when building. |
| 2026-10-27 | Tech — **one monorepo** (API, jobs, web, later iOS/Android, planning docs). |
| 2026-10-27 | The current Python Keeper Filters page **stays as it is until the app includes its filters**; then the Python code and page are removed (golden files saved first for the C# tests). |
| 2026-10-27 | Tech defaults confirmed (PLAN.md "Technical decisions"): minimal APIs + EF Core migrations, TanStack Query + i18next, own access/refresh tokens after Google sign-in, small model for simple AI questions, Application Insights, Blob backups, GitHub Actions CI/CD, PokeMiners sprites, Open-Meteo. **Stage 7 done.** |
| 2026-10-27 | Priorities adjusted by the owner: 1.7, 2.5, 2.12, 2.13, 6.7, 11.2, 11.3, all of 12 (data corrections) and all of 14 except 14.1 → Good to have; 5.2 → Future. |
| 2026-10-27 | **Bosses and events are fetched by the system**: the weekly AI research job (13.2, now MVP) reads sources such as **LeekDuck** (supersedes "no scraping of community sites") and **publishes automatically** with the source shown; the admin only fixes or removes entries (new MVP item 14.12). Terms of community sites to recheck before any public launch. |
| 2026-10-27 | Sign-off — **1. Pokédex** approved as described in `planning/SPECS.md`. |
| 2026-10-27 | Sign-off — **2. Pokémon page** approved; "where it appears right now" (2.6) moved to MVP. |
| 2026-10-27 | Sign-off — **3. Moves** approved as described. |
| 2026-10-27 | Sign-off — **4. Search filters** approved as described. |
| 2026-10-27 | Sign-off — **5. Tier lists** approved as described. |
| 2026-10-27 | Sign-off — **6. Raid bosses** approved as described. |
| 2026-10-27 | Sign-off — **7. Max Battles** approved as described (all after the MVP). |
| 2026-10-27 | Sign-off — **8. Battle simulator** approved as described (all after the MVP). |
| 2026-10-27 | Sign-off — **9. My collection** approved as described. |
| 2026-10-27 | Sign-off — **10. Best team** approved as described. |
| 2026-10-27 | Sign-off — **11. AI assistant** approved as described (MVP: answers from our data only, session-only chats, $5 cap shared with the researcher). |
| 2026-10-27 | Sign-off — **12. Data corrections** approved as described (all after the MVP). |
| 2026-10-27 | Sign-off — **13. Game calendar** approved as described. |
| 2026-10-27 | Sign-off — **14. Admin panel** approved as described (MVP: data status & re-run, fix fetched entries; admin role and AI cap in configuration). |
| 2026-10-27 | Sign-off — **15. Account and settings** approved as described (browse without sign-in; sign-in for collection, saved filters, AI). |
| 2026-10-27 | Sign-off — **16. Data import pipeline** approved as described. |
| 2026-10-27 | Sign-off — **17. Offline and installing** approved as described. |
| 2026-10-27 | Sign-off — **18. Team GO Rocket** approved as described (all after the MVP). |
| 2026-10-27 | Sign-off — **19. Eggs & research** approved; egg pools and research rewards with hundo CPs (19.1) moved to MVP. |
| 2026-10-27 | Sign-off — **20. Trading & friends** approved as described (all after the MVP). |
| 2026-10-27 | Sign-off — **21. Routes & Party Play** approved as described (all Future). |
| 2026-10-27 | Sign-off — **22. Mobile apps** approved as described. **All features signed off** (`planning/SPECS.md`); final priorities in `planning/PRIORITIES.md`: 56 MVP · 75 Good to have · 28 Future. **Stage 8 done.** |
| 2026-10-27 | The backlog (stage 9) is built **after the design** (merged with stage 11), since the design will change it; next is stage 10 (UI/UX). |
| 2026-10-27 | Design: every screen is shown at **phone and desktop size** before approval. |
| 2026-10-27 | Design: **app map approved** (`design/APP-MAP.md`) — tabs Today, Pokédex, Battle, Filters, Collection; search, Ask (AI) and profile always on screen; 25 MVP screens A1–H3. |
| 2026-10-27 | Design style (`design/source/CONVENTIONS.md`): clean & data-focused · light + dark following the device · **teal** accent (orange only for warnings) · type colours on badges, and **Pokémon and raid boss pages tinted by main type** · comfortable density · rounded font (**Nunito**) · one shared sample dataset with a fictional trainer (Ash_PT, level 48, Mystic) on Tue 27 Oct 2026 18:30. Game sprites fetched locally, not committed. |
| 2026-10-27 | Screen **A1 Today approved** (tiles for raids and Max Battles, live/next events, eggs & research summary, data-source line). Sprites trimmed and scaled to fill their box equally on all screens. |
| 2026-10-27 | Working agreement: questions are asked **as plain text in the chat**, answered freely (no multiple-choice pop-ups). |
| 2026-10-27 | Calendar entry points: calendar button in Today's header (A1 updated) and search offers the Calendar for "calendar", "raid hour", "community day" (H2). **A2 Calendar approved.** |
| 2026-10-27 | **No source information anywhere on screens**: no source lines, no ⓘ, no source badges (item 2.11 removed; specs and PLAN updated). AI chat answers may still cite web pages inside the answer (11.2). |
| 2026-10-27 | New MVP item **13.8**: event details show your copies of the featured Pokémon with evolve/keep/transfer advice. **A3 Event details approved.** |
| 2026-10-27 | Data freshness: **no "data as of" dates on screens; only an out-of-date warning** when data stops updating (16.3 changed, MVP). **A4 Eggs & research approved.** |
| 2026-10-27 | Pokédex list: **one row per species**, versions shown as markers (Mega, Shadow, Dynamax, G-Max, regional forms) and detailed on the Pokémon page; stats/filters use the base form; version filters (has Mega/Shadow, can Dynamax/Gigantamax, regional forms). Spec 1 updated. |
| 2026-10-27 | Pokédex list: lines show the base form; **hovering (desktop) or tapping (phone) a version marker switches the line to that version's stats**; same-stat versions (Shadow, Dynamax, G-Max) show a note. |
| 2026-10-27 | Pokédex filters **look at every version**: a Pokémon matches if any version matches; the line shows the best-matching version and **hides badges of versions that don't match**. |
| 2026-10-27 | **B1 Pokédex list approved** (one line per species, version markers with hover/tap switching, version-aware filters). |
| 2026-10-27 | All commits authored by the owner's GitHub account (history rewritten); Claude's involvement shown by the Co-Authored-By trailer. |
| 2026-10-27 | Pokémon page (B2) on the **phone uses tabs** (Overview · Moves · IVs · Get it · Versions), not one long page; desktop keeps everything on one page in two columns. |
| 2026-10-27 | **B2 Pokémon page approved** (type-tinted header, version switcher, phone tabs Overview · Moves · IVs · Get it · Versions; desktop two columns). |
| 2026-10-28 | **B3 Moves list approved** (Fast/Charged switch, type filter, sort; phone Raids/PvP number switch, desktop both side by side; PvP effects shown). The phone Pokédex tab gets a **Pokémon · Moves · Type chart** switcher (B1 updated). |
| 2026-10-28 | Move page (B4): learners **sorted by base Attack**, no computed score (a raid score was tried and rejected as an unexplained number); rare groups (Elite TM, Legacy) shown first. |
| 2026-10-28 | **B4 Move page approved** (raid and PvP numbers, Elite TM / Legacy / normal learner groups sorted by Attack, event note when an event gives the move). The owner likes a **raid score** for ranking, but it must be **explained, not a bare number** — to be designed with the raid counters (C2). |

## Open decisions

- None yet.
