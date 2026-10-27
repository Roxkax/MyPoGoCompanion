# MyPoGoCompanion — project context

A free companion system for Pokémon GO players: a shared API, a web app (first) and native mobile apps (later).
The repository today holds a working Python pipeline that builds copy-paste storage search strings ("Keeper Filters"); its full context is in `docs/KEEPER-FILTERS.md`. Read it before touching `scripts/`, `web/` or `build.py`.

We are **planning**, following the process in `docs/PLANNING-PROCESS.md` (stages 1–12). **Don't start application code until the owner says to start a sprint.**

## Working agreements

- **Ask one question at a time.**
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
| 6 Feature-by-feature review | **Next** |
| 7 Technical decisions | Mostly decided (see log); remaining ones confirmed here |
| 8 Prioritise (MVP / Good to have / Future) | — |
| 9 Backlog (Epic → Feature → Task, 100-hour sprints) | — |
| 10 Design: app map, then realistic screens one by one | — |
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
| 2026-10-27 | All commits authored by the owner's GitHub account (history rewritten); Claude's involvement shown by the Co-Authored-By trailer. |

## Open decisions

- None yet.
