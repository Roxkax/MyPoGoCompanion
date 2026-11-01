# MyPoGoCompanion — project context

A free companion system for Pokémon GO players: a shared API, a web app (first) and native mobile apps (later).
The repository today holds a working Python pipeline that builds copy-paste storage search strings ("Keeper Filters"); its full context is in `docs/KEEPER-FILTERS.md`. The pipeline lives in `legacy/keeper-filters/`; read that doc before touching it. The repository layout is in `README.md`.

Planning (stages 1–11 of `docs/PLANNING-PROCESS.md`) is done; we are **building in sprints** (stage 12). Work follows the backlog (`planning/BACKLOG.md`, GitHub issues, project board); progress is tracked in **Sprint progress** below. Start a new sprint only when the owner says so.

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
- **Owner review task in every PR:** a feature PR closes **all** of that feature's issues, including its "Owner review & testing" task and the parent feature issue (`Closes #n` for each in the PR description). **Merging the PR is the owner's approval** of that feature.
- **Progress tracking:** after every task, PR, merge or setup step, update **Sprint progress** below (status, PR, what's left, owner to-dos) and commit it together with the work, so this file always shows where the build stands.

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
| 10 Design: app map, then realistic screens one by one | Done 2026-10-28 (25 MVP screens + dark mode; `design/`) |
| 11 Rebuild the backlog from the design | Done 2026-10-28 (`planning/BACKLOG.md`, GitHub issues) |
| 12 Start sprints | **In progress** — Sprint 0 done 2026-11-01; **Sprint 1** (Foundations) current |

Earlier product work (feature list F01–F25 with priorities, UI choices, first wireframes) is archived in `docs/archive/` for reference only. It is **not** decided: the product is being redone with this process.

## Sprint progress

Board: https://github.com/users/Roxkax/projects/2 (kept in sync by the `Project sync` workflow). One PR per feature, closing all of its issues including the owner review task and the feature issue; the owner reviews, tests and merges, and **the merge is the approval** (no GitHub approval needed).

### Sprint 0 — Delivery pipeline (started 2026-10-28)

| Feature | Status | PRs | Notes |
|---|---|---|---|
| E00-F01 Monorepo layout and developer tooling | **Done** 2026-10-28 | #892, #894 | Tasks #3–#7 closed. Python pipeline moved to `legacy/keeper-filters/`; `.gitattributes` forces LF. The dev **seed data** is left for E01-F01 ("Initial migration and dev seed"). |
| E00-F02 Azure, Neon and GitHub environments | **Done** 2026-10-29 | #898, #899 | Issues #8–#14 closed. Owner set up Neon (`main`, `qa`), deployed `infra/main.bicep` to `rg-mypogocompanion` (West Europe) and created the GitHub environments `prod` and `qa`; **Environment check** green for both (OIDC sign-in, resources, database, Static Web Apps token). GitHub's OIDC subject carries numeric IDs: `repo:Roxkax@3799595/MyPoGoCompanion@1434430254:environment:<env>`. |
| E00-F03 Deployable API and web skeletons | **Done** 2026-10-30 | #901 | Issues #15–#22. API (`/health` with database check, `/health/live`, OpenAPI, CORS, accepts Neon's URL; chiseled Docker image on **port 8080**), web shell showing the API status (light/dark), xUnit v3 + Testcontainers + Vitest + Playwright tests, smoke test script and workflow. For E00-F06/F08: set the Container App target port to **8080** (bicep still says 80 for the placeholder) and set `ConnectionStrings__Database`, `Cors__AllowedOrigins`, `VITE_API_URL`. |
| E00-F04 Build & unit-test pipeline | **Done** 2026-10-31 | #902 | Issues #23–#27 closed (feature #23 closed by hand on 2026-10-31). Workflow "Build and unit tests": .NET format check, Release build, every `*.UnitTests` project; web lint, Prettier, type check, Vitest, build; job **`build-and-unit`** is the one to require. Coverage (MS CodeCoverage + Vitest v8, ReportGenerator local tool) posted as a PR comment; **minimum 90% of lines** for .NET and for the web app (owner, 2026-10-31). .NET unit coverage 15% → 95%, web 100%. Jobs project and golden files join the check when they exist (E02-F01, E00-F10). |
| E00-F05 Integration-test pipeline | **Done** 2026-10-31 | #903 | Issues #28–#31 closed (feature #28 closed by hand on 2026-10-31). New `api/MyPoGoCompanion.Data` (empty `AppDbContext`, empty `Baseline` migration, `dotnet-ef` tool); the schema goes there in E01-F01. `PostgresFixture` migrates a template database once per run; `CreateDatabaseAsync()` gives writing tests their own copy. Workflow "Integration tests", job **`integration`** (the one to require); results summary posted as a PR comment. |
| E00-F06 Shared QA deploy pipeline | **Done** 2026-10-31 | #904 | Issues #32–#39 closed (review task #39 and feature #32 closed by hand on 2026-10-31). Workflow "QA deploy", job **`deploy-qa`**: API image to GHCR `ghcr.io/roxkax/mypogocompanion-api:<commit>` → EF migration bundle on Neon `qa` → web to Static Web Apps environment `qa` → `api-qa` (secret `database-url`, port 8080, CORS) → smoke test → sticky PR comment ("QA now shows PR #n" / "moved on"). Leftover per-PR preview sites (numeric names) are removed on each deploy. Notes-only pushes skip the deploy (job "Anything to deploy?"; queue on the deploy job only). Web app deployed with the **SWA CLI** (the deploy action ignores the environment name on PR runs and made a per-PR site); migrations use Neon's direct host. Concurrency group `qa-environment`; drafts and forks not deployed; PR head commit deployed. |
| E00-F07 UI-test pipeline | **Done** 2026-10-31 | #905 | Issues #40–#44 closed (review task #44 and feature #40 closed by hand on 2026-10-31). Playwright runs **inside `deploy-qa`** after the smoke test (so it holds the QA queue); job **`ui-tests`** (the one to require) turns the result into its own check (red on failure or when the deploy stopped first; skipped when notes only). Trace, screenshot and video kept per failed test (artifact `ui-test-report`, 14 days); summary via `.github/scripts/ui_results.py`; QA comment shows the UI result. Smoke journey `tests/ui/specs/journey.spec.ts` (open, API, device theme switch); **tab and in-app theme steps join with E01-F03** (no tabs yet). **QA deploy is now opt-in with the `qa` label** (also in #905): label on = deploy + UI tests on that push and every later one; no label = both checks skipped (count as passed); drafts follow the label too. |
| E00-F08 Prod deploy on merge to main | **Done** 2026-10-31 | #906 | Issues #45–#50 closed by the PR. **First prod run green** (run 1, 2026-10-31: deploy, smoke test, QA database reset, no alert). Workflow "Prod deploy" on every push to `main` (skips notes-only changes since the last successful `prod` deployment): image → migrations on Neon `main` → `api-prod` → Static Web Apps production → smoke test against prod. Then **Reset the QA database** (Neon reset from parent; repo secret `NEON_API_KEY`, variable `NEON_PROJECT_ID`; waits in the `qa-environment` queue; redeploys the PR QA was showing). Failures open/add to the issue **"Prod deploy failed"** (label `prod-alert`, assigned to the owner), closed by the next good deploy. **Rollback**: Run workflow with an earlier commit of `main` (`docs/PROD-DEPLOY.md`). Only runs from `main`, so the merge is its first real run. All three backend choices (issue alerts, notes-only skip, redeploy of the open QA PR after the reset) confirmed by the owner. **Environment check** now also checks the Neon API key, that `qa` is a child of `main` and that the environment's `DATABASE_URL` is that project's branch (read only). **Step 6 done by the owner and verified 2026-10-31** (qa run green): real Neon project is **`mypogocompanion` (`soft-lab-02601198`)**. |
| E00-F09 Branch rules and PR hygiene | **Done** 2026-10-31 | #907 | Issues #51–#56 closed by the PR. **Branch rules check green on `main`** after the merge. From now on everything, planning documents included, reaches `main` only through a PR. Ruleset **Protect main** as code (`.github/rulesets/main.json`: PRs only, the four required checks pinned to GitHub Actions, branch up to date, conversations resolved, no force push/deletion, no bypass, 0 approvals), switched on by the owner importing it in Settings → Rules; **Branch rules check** workflow compares GitHub with the file (on change, weekly, by hand). PR template with a testing checklist. Dependabot (NuGet, npm, Actions; weekly Monday; minor+patch grouped; no major updates for the .NET 10 framework packages); Dependabot runs skip PR comments and the QA deploy (no secrets). CodeQL (C#, TypeScript, Python, Actions; not required). Setup in `docs/BRANCH-RULES.md`. **Ruleset imported by the owner 2026-10-31** (id 26290614, active, no bypass) and matches the file. **Proof done**: PR #908 with a deliberately failing unit test → `build-and-unit` red, merge blocked (owner saw the disabled merge button); #908 closed unmerged. #907 itself green under the rules (CodeQL green in all four languages). Dependabot **weekly** confirmed by the owner. Next: E00-F10 (golden files). |
| E00-F10 Golden files from the Python keeper build | **Done** 2026-11-01 | #919 | Issues #57–#62. `tests/golden/keeper-filters/`: frozen **inputs** (PvPoke `c1d4bfb`, game master `b4a31cc`, trimmed to what the pipeline reads: 2.3 MB) and **outputs** (PvP boxes/strings, raid rankings, page data, search-simulator cases), `manifest.json` with SHA-256. `legacy/keeper-filters/scripts/golden.py freeze|check`. C# harness `tests/MyPoGoCompanion.GoldenFiles` (`GoldenAssert.Matches`, path-level diff, optional relative tolerance) + `GoldenFiles.UnitTests` (harness + integrity of the frozen files). New `build-and-unit` job "Python keeper build reproduces the golden files". All three choices confirmed by the owner (2026-11-01): **trimmed inputs committed**, **exact comparison by default**, **Python check stays inside the required `build-and-unit`**. Merged by the owner 2026-11-01; issues #57–#62 closed. Chandelure (609) still in `SOON` (left as is). **All Sprint 0 features done**; epic #1 closed 2026-11-01 (no Sprint 0 issue left open). |

### Sprint 1 — Foundations

| Feature | Status | PRs | Notes |
|---|---|---|---|
| E01-F01 Database schema v1 | **Done** 2026-11-01 | #920 | Issues #64–#72. Migration `SchemaV1` (32 tables) + `docs/DATABASE.md`: game data with the game's own ids (forms = PvPoke ids; Shadow/Dynamax/G-Max are flags, not rows), rankings (only **rank-1 PvP IVs** stored), live data with researcher key, source snapshot, admin fix lock and removal, users/collections (upload file not kept; per-upload diffs kept), job runs, AI usage, admin fixes. snake_case via `EFCore.NamingConventions`; schedule times are wall-clock (`timestamp without time zone`, `time_zone` null = local). Dev seed: real slice of 91 species from the golden inputs + the design's sample live data and Ash_PT, dates moved to today; the API migrates and seeds in Development only. New `Data.UnitTests` (27) and 15 new integration tests (deletes, checks, seed, dev start-up). All four backend choices confirmed by the owner (2026-11-01): rank-1 IVs only; upload file not kept; every upload's changes kept; removed entries stay removed. Merged by the owner 2026-11-01; issues #64–#72 closed. Next: E01-F02 Google sign-in, tokens and roles. |
| E01-F02 Google sign-in, tokens and roles | **In progress** (started 2026-11-01) | — | Issues #73–#83, branch `claude/e01-f02-google-sign-in`. Backend being confirmed with the owner: refresh token in browser storage (confirmed); super admin + roles (being worked out). |

### Other work done

| Date | Work | PRs |
|---|---|---|
| 2026-10-28 | Project board created and synced: all 891 backlog issues added, Status (Todo · In progress · In review · Done) and Hours set; views Current sprint, Backlog, Features. Script handles GitHub's auto-added sub-issues and retries its temporary errors. | #893, #895, #896 |
| 2026-10-31 | First Dependabot batch (major updates) rebased one by one onto the latest `main` and **rebase-merged** (owner): `github-script` 9, `upload-artifact` 7, `checkout` 7, `setup-dotnet` 6, `azure/login` 3 (tested on QA first with the `qa` label), `jsdom` 30, TypeScript 7 in `tests/ui/`; also #909. Every prod deploy green (prod on `ab9b482`). TypeScript 7 in `web/` (#911) failed `npm ci`: `typescript-eslint` 8.72 needs TypeScript < 6.1 (TS 7.0 has no API); fixed **side by side** (owner): `tsc` is TypeScript 7 (alias `@typescript/native`), the `typescript` package is `@typescript/typescript6` for ESLint (`web/README.md`). | #909, #910, #912–#917, #911 |
| 2026-11-01 | Merge rule: merge commits only, required check **`rebased`** (branch rebased on the latest `main`, no merge commits). | #918 |
| 2026-11-01 | **Fix: prod and QA deploys after #920 failed** at "Apply migrations" (`42703: column "migration_id" does not exist`, alert issue #922): the snake_case naming also renamed EF's history-table columns, which QA and prod created earlier as `"MigrationId"`/`"ProductVersion"`. The history table now keeps EF's names (`MigrationHistory.cs`); new integration test migrates a database made the old way, and a real migration bundle was checked against one. Prod stayed on the previous version (migrations run before the API deploy). | #923 |
| 2026-11-01 | **QA and local copies of the web app are marked** (owner's request): orange "QA" strip with the PR number and commit, "QA" pill, orange tab icon and "QA · " tab title; grey "Local" for the dev server; nothing in production. Build setting `VITE_APP_ENV` set by the QA and prod deploys. Also: Sprint 0 epic #1 closed, Sprint 1 is the current sprint (`project-sync.js` view default). | #921 |
| 2026-11-01 | **The web app keeps the refresh token in browser storage** (owner, option A): web app and API are on different sites (`*.azurestaticapps.net`, `*.azurecontainerapps.io`), so a cookie scripts can't read would be a third-party cookie that Safari and Firefox block. Protected by a 15-minute access token kept in memory, refresh-token rotation with reuse detection (a reused token ends that sign-in) and a strict Content Security Policy; the same model the mobile apps will use. Move to a cookie scripts can't read when a custom domain is added. Not chosen: custom domain now (~€10–20/year); partitioned third-party cookie (unreliable in Safari). |

### Owner to-dos

- Replace the `PROJECT_TOKEN` secret with a new classic token that has **only** `project` + `public_repo`, then delete the old token (the current one carries admin and delete-repo scopes).
- Add the **`qa` label** to a PR when you want it on QA.
- PR #918 (merged): if not done yet, edit the ruleset **Protect main** in Settings → Rules: merge methods → only **Merge**, add the required check **`rebased`** (GitHub Actions). In Settings → General → Pull Requests: only **Allow merge commits**, default message **Pull request title and description**, **Always suggest updating pull request branches** on. Then run **Branch rules check**.
- E00-F09 (merged): delete the branch `claude/proof-failing-test` (button on closed PR #908; Claude's git proxy can't delete branches) and the merged Dependabot branches, or switch on **Settings → General → Automatically delete head branches**. Switch on **Dependabot alerts** and **Dependabot security updates** (Settings → Advanced Security); leave CodeQL default setup off. Still to confirm: CodeQL stays out of the required checks.
- E00-F08 (merged): first prod run is green. Still to do from its testing steps: the **rollback test** (`docs/PROD-DEPLOY.md`) and **Environment check** for `prod` on `main` (confirms the prod `DATABASE_URL` is branch `main` of the same project).
- Neon: the old `qa` branch had **auto-delete** on and was removed; it was re-created from `main` with auto-delete **Never**. The real project is `mypogocompanion` (`soft-lab-02601198`, the one the secrets and the QA reset use); **delete the other Neon project**.
- Optional: delete the stale branches `claude/dreamy-ptolemy-lvqw25` and `claude/project-board` (their commits are already on `main`).
- Clone the repo (`https://github.com/Roxkax/MyPoGoCompanion.git`) to `C:\Dev` and set `git config user.name/user.email` (see Git above).
- **Sprint 1 is current**: on the board, open the **Current sprint** view, change its filter from `milestone:"Sprint 0"` to `milestone:"Sprint 1" -label:"type: epic"` and click **Save** (GitHub's API can't edit a view, so this one is by hand). Optionally close the **Sprint 0** milestone (Issues → Milestones). Do the same at each sprint change.

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
| 2026-10-28 | **B5 Type chart approved** (phone: Defending · Attacking · Full chart; desktop: picker + full chart; GO multipliers ×1.6 / ×0.63 / ×0.39, no immunities). Pokémon page matchups link to the type chart (B2 updated). |
| 2026-10-28 | Battle tab has a **Raids · Tier lists · Teams** switcher. Raid boss list shows **100% CP (normal and boosted) on phone and desktop**, plus weaknesses, shiny marker and per-boss end dates when they differ. |
| 2026-10-28 | **C1 Raid bosses approved** (Now / Coming up, tier filter, weaknesses, 100% CP, shiny, event pills). |
| 2026-10-28 | **C2 Raid boss page approved** (phone tabs Counters · Yours · Boss · Catch; desktop two columns). Raid score shown with **damage/s, how long it lasts and % of best**, plus a "How the raid score works" explainer. The C# raid engine **models the boss's moves** so counters re-rank when the user picks them (spec 6.3). |
| 2026-10-28 | **C3 Tier lists approved** (PvP Great/Ultra/Master with S/A/B/C tiers by stated score ranges; "Only what I own" shows your best copy; raid lists per type with a pinned Mega slot and the C2 number format). PvPoke credit (MIT) goes in **Settings → About**, not on screens. |
| 2026-10-28 | **C4 Best team approved** (By type / Against a boss; Best possible / From my Pokémon; locked Mega slot; weak slot flagged when under 40% of the best and nothing better is left). |
| 2026-10-28 | **D1 Keeper filters approved** (Filters tab switcher Keepers · Builder · Saved; one card per filter with suggested tag; phone shows Copy buttons per part, desktop shows previews and how-to). **Safe to transfer works from the user's tags** (`!#GL&!#UL&!#Raid&!#Max&!#Mega&…`); spec 4 updated. |
| 2026-10-28 | **D2 Filter builder approved** (condition rows joined by AND, values inside a condition OR; add-a-condition sheet with 10 groups and search; live string, hand-editable, checked by the game-search simulator, length shown, auto-split; Save and Copy). |
| 2026-10-28 | **D3 Saved filters approved** (list with Copy and ⋯ menu: open, rename, duplicate, delete; save sheet with name; stored with the account, sign-in needed). |
| 2026-10-28 | New Good to have item **9.10 "Your Pokémon" edits** (power up, evolve, change moves, transfer, marked "Edited in app"). The **Poke Genie upload stays the absolute source of truth** for now; a better way to keep the collection updated is to be planned later (open decision). Totals: 56 MVP · 76 Good to have · 28 Future. |
| 2026-10-28 | **E1 My collection approved** (summary + Upload new, filters/sort, league chips and raid % chip per Pokémon, first-time Poke Genie upload state). League ranks show **only for the top 2% (within 98% of the best IVs)**, the same rule as the keeper filters. |
| 2026-10-28 | **E2 Upload & import report approved** (import progress steps; report with imported / not recognised / new · gone / changed; unrecognised rows with reasons; what changed, matched by family + IVs + catch date) and its **"All changes" screen** (filter chips by kind; changed Pokémon first with before → after; new and gone grouped by species). |
| 2026-10-28 | **F1 Sign in approved** (sign-in screen/modal with what works without an account, four benefits, Continue with Google only, "Not now", privacy + fan-project disclaimer; signed-out prompts in place of personal screens; Admin hidden unless the admin is signed in). |
| 2026-10-28 | **F2 Settings approved** (account with Admin badge, language EN / pt-PT, theme, About & credits with disclaimer and data credits incl. PvPoke MIT, install the app, Admin row on phone for the admin, sign out). Delete account / download data stays Good to have but is **required before any public launch**. |
| 2026-10-28 | **G1 Data status approved** (one card per job with last/next run, what changed, Run now; AI spend this month vs the $5 cap; recent runs with failures explained; preview of the user warning). Out-of-date warning thresholds: **3 days for daily game data, 9 days for raids and events**; spec 16 updated. |
| 2026-10-28 | **G2 Fix fetched entries approved** (entries by kind with New / Changed this week / Fixed by you flags; source links shown here only, admin-only; edit sheet with where it came from, fields, Save fix, Remove). **Fixed entries are locked**: the weekly AI run won't overwrite them; if the source later changes, the entry shows "Source changed since your fix"; spec 14 updated. |
| 2026-10-28 | **H1 AI chat, H2 Search and H3 Offline approved** (chat full screen on phone, side panel on desktop; answers end with "Based on" links to the screens used; says when the data has no answer; paused message at the $5 cap. Search full screen on phone and Ctrl K window on desktop, grouped results, "raid hour"/"calendar" offer the Calendar. Offline screen with Try again). **All 25 MVP screens designed**; remaining in stage 10: dark-mode check. |
| 2026-10-28 | **Dark-mode check done**: all 25 screens rendered in dark (`design/screens/*-dark.png`, overview `DARK-overview-phone.png`); fixed pastel chips (Shadow, G-Max, Dynamax, lucky, 5★, raid %, gone, errors, type-chart cells) got dark versions. The Google sign-in button stays white (Google's branding rules). |
| 2026-10-28 | CI/CD set up **before the backlog** (as Sprint 0 "Delivery pipeline"). **Prod** auto-deploys web + API on merge to `main`. **One shared QA environment** (web + API + its own database) that shows the most recently updated open PR; deploy + UI tests for QA run one PR at a time (queued) so PRs don't overwrite each other mid-test. Pipelines: build & unit tests, integration tests, UI tests, QA deploy; **all required to merge**; no direct pushes to `main`. Repo is public, so branch protection and Actions minutes are free. |
| 2026-10-28 | **QA database = a copy of prod**, reset automatically to a fresh copy every time `main` deploys to prod (Neon branch reset). UI tests run on QA against that real data, so they check behaviour (screens load, flows work, numbers present and consistent) rather than fixed sample values. |
| 2026-10-28 | Merge rule: **no approval required while the owner works alone** (all checks green, branch up to date, conversations resolved). When collaborators join: 1 approval + CODEOWNERS for pipeline/infra files. Full setup in `planning/CI-CD.md`. |
| 2026-10-28 | Branch rules on `main` are **switched on at Sprint 0**, together with the pipelines; until then planning documents keep going straight to `main`. |
| 2026-10-28 | Web addresses: **Azure's free default addresses** for prod and QA for now; a custom domain (~€10–20/year) can be added later without other changes. **CI/CD planning done** (`planning/CI-CD.md`). |
| 2026-10-28 | Stage 11 started: backlog as code (`planning/backlog/*.json` → `build.py` → `planning/BACKLOG.md`; GitHub workflows `backlog-import` / `backlog-reset`). Every MVP / Good to have feature carries unit, integration and UI test tasks plus an owner review & testing task (15%); a Quality epic adds journeys, visual regression, accessibility, performance, security, mutation testing. Sprints filled task by task to 100 h; Good to have starts a fresh sprint. Draft: MVP 1,307 h (Sprints 0–13), Good to have 1,022 h (14–24), Future 480 h (not scheduled). |
| 2026-10-28 | **Backlog approved** by the owner; imported into GitHub issues with the `Backlog import` workflow. **Stage 11 done.** Next: Sprint 0 only when the owner says so. |
| 2026-10-28 | **Sprint 0 started** (stage 12). Code goes through pull requests from the working branch; planning-only changes may still go to `main` until the branch rules are on. |
| 2026-10-28 | Monorepo layout (task #3): `api/`, `jobs/`, `web/`, `tests/`, `design/`, `planning/`, `docs/`; unit tests next to each project, integration/UI tests and golden files in `tests/`. The Python Keeper Filters pipeline **moved to `legacy/keeper-filters/`** (unchanged output; `docs/KEEPER-FILTERS.md` stays in `docs/`) so the new app gets `web/`. |
| 2026-10-28 | **One pull request per feature**, holding all of that feature's tasks; the owner reviews and tests it once. Issues are closed by the PR (`Closes #n`). |
| 2026-10-28 | Sprint 0 tooling (E00-F01): .NET 10 SDK pinned in `global.json`, shared `Directory.Build.props` (nullable, latest analyzers, warnings as errors in CI), `.editorconfig` code style checked by `dotnet format`; web lint with ESLint 10 + typescript-eslint and Prettier; local **PostgreSQL 17** (Neon's version) in Docker Compose. Dev seed data comes with the schema (E01-F01). |
| 2026-10-28 | **Project board**: GitHub project "MyPoGoCompanion" created and kept in sync by the `Project sync` workflow (token secret `PROJECT_TOKEN`). Columns Todo · In progress (draft PR open) · In review (PR ready) · Done; Hours field on tasks; sprints = milestones. Feature PRs are merged by the owner **without an approval** (GitHub doesn't allow approving your own PR). |
| 2026-10-28 | **E00-F01 done** (PRs #892, #894; issues #2–#7 closed). Progress is tracked in CLAUDE.md's **Sprint progress** section, updated with every task, PR and setup step. |
| 2026-10-29 | Repository **renamed to `Roxkax/MyPoGoCompanion`** by the owner (old URLs redirect). Azure's GitHub sign-in uses the new name. |
| 2026-10-29 | **E00-F02 done**: environments set up and checked. As deployed by the owner: container images in **GitHub Container Registry** (no Azure registry, ~€5/month saved), Azure **West Europe** + Neon **AWS Frankfurt**, **€5/month Azure budget alert** to the owner's email, logs capped at 0.5 GB/day, GitHub → Azure sign-in by OIDC with Contributor on the resource group only. |
| 2026-10-31 | **Unit-test coverage minimum: 90% of lines**, checked separately for .NET and for the web app; the `build-and-unit` check fails below it. Generated code and `Program.cs` (startup wiring, covered by integration tests) don't count. |
| 2026-10-31 | **Interactive API docs page** (Scalar or Swagger UI over `/openapi/v1.json`) comes with **E01-F05**, shown on **QA and local only**, not prod (prod's API is public). |
| 2026-10-31 | QA deploy **skips pushes that only change notes** (Markdown, `docs/`, `planning/`, `design/`) since what QA shows for the PR; the skipped `deploy-qa` still counts as passed. |
| 2026-10-31 | **QA deploy is opt-in with a `qa` label** (replaces "every push to a PR deploys" and makes QA testing before a merge optional): adding the label deploys the PR and runs the UI tests, every push while it's on deploys again, removing it stops that. Without the label `deploy-qa` and `ui-tests` are skipped, which branch rules count as passed. "Run workflow" by hand stays as a one-off option. `planning/CI-CD.md` updated. |
| 2026-10-31 | **Every feature PR closes all of that feature's issues, including the "Owner review & testing" task** (and the parent feature issue); **merging the PR is the owner's approval**. Review tasks and feature issues left open by earlier merged PRs (#23, #28, #32, #39, #40, #44) were closed. |
| 2026-10-31 | **Prod deploy failures alert the owner with a GitHub issue** "Prod deploy failed" (label `prod-alert`, assigned to the owner), closed automatically by the next good deploy; no email service (owner). |
| 2026-10-31 | **Prod deploy skips merges that only change notes** (Markdown, `docs/`, `planning/`, `design/`) since what prod runs, the same rule as QA; **Run workflow** by hand always deploys (owner). |
| 2026-10-31 | After the QA database reset, the pipeline **redeploys the PR QA was showing, only if that PR is still open** (so its own migrations are back); nothing extra runs when QA showed the PR just merged (owner). |
| 2026-10-31 | **Dependabot runs weekly** (Monday 07:00 Lisbon) for NuGet, npm and GitHub Actions: minor and patch updates grouped per ecosystem, major updates one PR each; no major updates for the .NET 10 framework packages, Npgsql and `dotnet-ef` (owner). |
| 2026-10-31 | **TypeScript 7 side by side with 6 in `web/`** (owner, option 2 of 2): `tsc` (type check, build) is TypeScript 7 via the alias `@typescript/native`; the `typescript` package is `@typescript/typescript6` so `typescript-eslint` keeps working; drop the alias when `typescript-eslint` supports TypeScript 7. Not chosen: staying on TypeScript 6 with a Dependabot ignore rule. |
| 2026-11-01 | **PRs are merged with a merge commit only, and the branch must be rebased on the latest `main` first** (owner; replaces "all three merge methods allowed"): squash and rebase merges switched off in the ruleset; new required check **`rebased`** fails when the branch is behind `main` or contains merge commits (update branches with **Update with rebase**); the owner writes the merge commit message. |
| 2026-11-01 | **Golden-file inputs are committed** (owner): the source data the Python keeper build read (PvPoke gamemaster, CP multipliers and rankings order; Niantic game master cut to move and Mega templates; 2.3 MB) lives in `tests/golden/keeper-filters/inputs/` with the outputs, so golden tests run offline and the C# port reads the same bytes. Includes a datamined game-master excerpt: fine for the personal fan project, **recheck before any commercial launch**. Not chosen: downloading the pinned versions at test time. |
| 2026-11-01 | **The C# port must match the golden files exactly by default** (owner): strings, boxes, lists and integers character for character; a tiny relative tolerance (e.g. 1e-12) only where floating-point maths proves to differ in the last digits (raid DPS, TDO, score), added in the test that needs it, never globally. |
| 2026-11-01 | **The Python golden-file check is part of the required `build-and-unit` check** (owner): the job "Python keeper build reproduces the golden files" rebuilds from the frozen inputs on every PR, so a change to the Python output blocks the merge until the golden files are refreshed on purpose (`golden.py freeze`). Stays until the C# port matches and the Python pipeline is removed. Not chosen: a separate, non-blocking workflow. |
| 2026-11-01 | **Only the rank-1 PvP IVs are stored** per form and league (owner, option A): the IV rank checker and "your copies" rank all 4,096 spreads on demand (about a millisecond). Not chosen: storing every spread (about 21 million rows, over Neon's free 0.5 GB; ~$19/month). |
| 2026-11-01 | **The uploaded Poke Genie file is not kept** (owner, option A): an upload becomes the collection rows, the unrecognised rows with their reasons, and the changes since the previous upload; better matching later means uploading again. Not chosen: storing every uploaded file (~1 MB each) for automatic re-matching. |
| 2026-11-01 | **Every upload's changes are kept** (owner, option A): new / gone / changed per upload stay in the database (a few KB each), so the later "Import history" feature has the full history from the first upload. Not chosen: keeping only the latest upload's changes. |
| 2026-11-01 | **An entry the admin removes stays removed** (owner, option A): it is hidden everywhere and the weekly AI researcher doesn't add it back, even if the source still lists it, the same lock as fixed entries; un-removing comes with the admin screen (E10-F02). Not chosen: removal lasting only until the next weekly run. |
| 2026-11-01 | **QA and local copies of the web app are marked** (owner's request; look confirmed): QA shows an orange strip "QA · Test version, not the real app · PR #n · commit", a "QA" pill next to the name, an orange tab icon and the tab title "QA · MyPoGoCompanion"; the dev server the same in grey with "Local"; production nothing extra. Set at build time with `VITE_APP_ENV` (an unmarked build counts as local, never as prod). |

## Open decisions

- **Keeping the collection up to date** (raised 2026-10-28): the owner doesn't always rescan in Poke Genie after evolving or powering up. For now the upload is the source of truth; plan a better approach later (e.g. in-app edits merged with uploads, matching rules between exports).
