# MyPoGoCompanion — plan

Status: **draft for the feature-by-feature review (stage 6)**. Nothing here is final until it is confirmed in the review and logged in `CLAUDE.md`. Priorities (MVP / Good to have / Future) come later, in stage 8.

## Vision

One place to answer the questions a Pokémon GO player has every day — *what is this Pokémon good for, should I keep it, what do I bring to this raid, how many people do I need, what's on this week* — using real game data, the player's own collection and an AI that learns what the data is missing.

- **Who for:** mainly the owner at launch; designed for many users so it can open up later.
- **What the owner plays:** raids, PvP, Max Battles and collecting — all equal.
- **Where:** web app + API first; native iOS, then Android, later.
- **Languages:** English and Portuguese (pt-PT) interface. Search strings in English (the owner's game language).
- **Cost:** free for users; near-zero running cost; AI capped at $5/month.

## Principles

1. **Real data first.** Stats, moves and evolution rules come from the game files; rankings from PvPoke and our own calculations. Anything not in the files (current bosses, events, special conditions) is proposed by the AI and approved by the admin.
2. **The AI proposes, the admin decides.** Nothing the AI finds changes the data until the admin approves it.
3. **Your collection makes it personal.** Every list that can be filtered by "what I own" will be, once the Poke Genie export is imported.
4. **Honest numbers.** Estimates (minimum players, Max Battle simulations) are labelled as estimates; every fact shows its source.

---

## Features

Each feature lists what you can do, what the backend needs, where the data comes from, and feasibility notes.

### 1. Pokédex

**You can:** browse every Pokémon (all 1,025 species and their forms, Megas, Shadows), search by name, number or nickname, and filter by type, generation, stats (Attack, Defense, Stamina, max CP), moves, Shadow/Mega/Dynamax/Gigantamax, shiny availability, legendary/mythical, released or not, and "in raids now". Sort by any column.
**Backend:** species/form/move tables built from the game master and PvPoke; a search endpoint with filters and sorting; precomputed max CP and rankings per form.
**Data:** game master (stats, forms, moves), PvPoke (Elite TM / legacy flags, rankings), approved AI facts (availability).
**Notes:** forms share a Pokédex number in the game; the app shows each form separately.

### 2. Pokémon page

**You can:** see everything about one Pokémon:
- base stats, max CP at levels 40/50/51 and the 100% CP cheat sheet (research, raids, weather boost, wild);
- the full movepool with raid and PvP stats, Elite TM, legacy and event-only moves;
- the evolution chain with every condition (candy, items, buddy distance, trade discount, day/night, lures, gender, special tasks);
- **how to get it**, including special conditions (e.g. "only from 10 km eggs", "evolve while it's raining");
- **every version it has or had**: regional forms, costumes (incl. retired), shiny, Shadow/Purified, Mega, Dynamax/Gigantamax — and changes over time (moves added or rebalanced);
- its rankings (PvP leagues, raid rank per type, Max Battle rank);
- its best IVs per league with a one-tap search string;
- where each fact comes from (game files, PvPoke, admin-approved), with a "report wrong data" button.

**Backend:** a Pokémon detail endpoint; a **history store** of game master changes (backfilled from the game master's public git history); a **curated layer** for approved facts that survives daily imports.
**Data:** game master + its history, PvPoke, curated layer (AI proposals approved by the admin).
**Notes:** "how to get it" and special conditions are only partly in the game files; the AI research and the chat fill the gaps, with your approval.

### 3. Moves

**You can:** list all fast and charged moves with raid and PvP stats side by side, filter by type, see who learns each move (normal, Elite TM, legacy) and how it changed over time; Max Moves and G-Max moves with their power.
**Backend:** move tables (PvE and PvP stats are different in the game), move history.
**Data:** game master, PvPoke.

### 4. Search filters

**You can:**
- copy ready-made filters that find your keepers: Great League, Ultra League, raid attackers, Max Battle attackers, Megas (today's Keeper Filters, rebuilt automatically), plus the inverse "safe to transfer";
- copy a short string for any one family from its Pokémon page;
- build your own filter with a form (Pokédex numbers, types, IV bars, CP, stars, shiny, lucky, Shadow, keywords) and see the string update live; options like keeper % (95/98/99) and level cap 50/51;
- paste any string and get a plain-language explanation plus the Pokémon it matches;
- preview which of **your** Pokémon a string matches; save and name your filters.

**Backend:** the Keeper Filters engine ported to C# (box search, encoder, game-search simulator, 1,950-character split), checked against golden files from today's Python build.
**Data:** game master, PvPoke rankings.
**Notes:** strings use English keywords; the game can't tell regional forms apart by Pokédex number (a game limitation).

### 5. Tier lists

**You can:** see ranked lists for:
- **PvP:** Great, Ultra, Master League and cups (from PvPoke), grouped into tiers;
- **Raids:** best attackers per type and overall (our DPS/TDO model, with the "best Mega" slot and the numbers behind each score);
- **Max Battles:** best Dynamax/Gigantamax attackers, tanks and supports.

Filter any list to "only what I own".
**Backend:** daily PvPoke import; raid ranking engine (ported from Python); a new Max Battle ranking model.
**Notes:** the Max Battle model is new work and less documented by the community; it will be labelled as an estimate at first.

### 6. Raid bosses

**You can:** see current and upcoming raid bosses by tier (1, 3, 5, Mega, Shadow, Elite) with dates and shiny availability; open a boss to see its types, weaknesses, possible moves, 100% CP (normal and weather-boosted), **best counters** (re-ranked when you pick the boss's actual moves), **your best counters**, **tips**, and the **estimated minimum players**.
**Backend:** a counters engine (raid model + type effectiveness + the boss's moves), the raid simulator (feature 8) for minimum players, a boss schedule table.
**Data:** game master (boss stats and moves), the weekly AI research job (feature 13) for the rotation, tips written or approved by the admin.

### 7. Max Battles

**You can:** see the current Max Battle bosses with their counters, your best Dynamax/Gigantamax Pokémon for each, and tips.
**Backend:** Max Battle counters from the Max model; schedule from the weekly AI research.
**Notes:** same estimate caveat as feature 5.

### 8. Battle simulator (raids and Max Battles)

**You can:** pick a boss and a team (generic best, or from your collection) and simulate: time to win, faints, and how many trainers are needed; adjust level, weather and friendship.
**Backend:** a raid simulation engine (boss moves, energy, dodging off, re-lobbies) and a Max Battle simulation engine; both run in the API.
**Notes:** no PvP simulator (PvP rankings come from PvPoke). Max Battle results are estimates.

### 9. My collection

**You can:** upload your Poke Genie export and see all your Pokémon with their IVs, level and moves; search and sort them; see each one's PvP rank per league and raid value; get suggestions (what to power up, what's safe to transfer, which Pokémon you're missing); see stats (types, shinies, luckies, hundos). Uploading a newer export replaces the old one and shows what changed.
**Backend:** CSV parser and name/form matcher (with a report of rows that didn't match), collection tables per user, recalculated values.
**Data:** your Poke Genie export only (there is no official game API).

### 10. Best team

**You can:** get the best 6 for each attacking type (one Mega slot), the best team against a specific raid or Max boss, and the best team **from your own collection**; save teams.
**Backend:** team builder over the raid/Max models and the collection.

### 11. AI assistant

**You can:** ask anything about the game in English or Portuguese. It answers from our data first (Pokédex, rankings, bosses, simulations, your collection) and searches the web for what we don't have, citing sources. Your conversations are saved.
**Backend:** Claude via Anthropic's API behind a provider-agnostic layer; tools that query our data; web search; per-month spending cap ($5) with usage shown in the admin panel.

### 12. Data corrections (AI → admin approval)

**You can (as admin):** see a queue of proposed changes — from the AI chat, from the weekly research job and from "report wrong data" — each with the current value, the proposed value and sources; approve, edit then approve, or reject. Approved facts appear on the Pokédex with an "admin-verified" badge.
**Backend:** proposal records (field-level), the curated layer, conflict detection when a later import changes the same field, audit log.

### 13. Game calendar (bosses and events)

**You can:** see current and upcoming raid rotations, Max Battle weeks and events (Community Days, Spotlight Hours, raid days, seasons), with featured Pokémon and bonuses; a home page with "today in the game".
**Backend:** the **weekly AI research job** reads news and proposes the rotation and events; the admin approves; manual edits possible.
**Notes:** weekly means mid-week surprises can be missed; the admin can also run it on demand.

### 14. Admin panel

**You can (as admin):** see the data import status (last run, errors, re-run), review proposals (feature 12), edit any data directly (logged), manage hand-maintained lists (e.g. Megas not in GO yet, boss tips), see AI usage and cost and set the cap, manage users and roles, read the audit log.

### 15. Account and settings

**You can:** sign in with Google; set your language (English / Português), theme (light/dark), preferred league; delete your account and download your data.
**Backend:** Google token validation, own session tokens, roles (User, Admin), settings per user.
**Notes:** the iOS app will also need Sign in with Apple.

### 16. Data import pipeline (behind the scenes)

Daily import of the game master and PvPoke, change detection, history, and recalculation of rankings, filters and counters — each verified before it's published.

### 17. Offline

The Pokédex, filters, raid counters and your collection stay readable without a connection (cached on the device). Changes and the AI need a connection.

---

## Architecture

```
 Game master (git) ─┐                                     ┌──────────────┐
 PvPoke (git) ──────┼─► Import worker (.NET, daily) ─────►│              │
 Weekly AI research ┘   history · recalculation · verify  │  PostgreSQL  │
                                                          │   (Neon)     │
 Claude API ◄──► API (.NET, ASP.NET Core, OpenAPI) ◄─────►│ game data,   │
                 engines: filters, raid/Max model,        │ curated layer│
                 counters, simulators, collection,        │ users, AI    │
                 AI tools, admin                          └──────────────┘
                        ▲
        ┌───────────────┼──────────────────┐
   Web app (React)   iOS (SwiftUI)    Android (Kotlin)
   MVP               later            later
```

- **Hosting:** API and worker on Azure Container Apps (scale-to-zero), web on Azure Static Web Apps, database on Neon. Container images on GitHub Container Registry (free).
- **Clients:** generated from the OpenAPI spec (TypeScript, later Swift and Kotlin).
- **Offline:** the web app caches read-only data in the browser; native apps cache on the device.

## Monthly cost estimate (personal use)

| Item | Cost |
|---|---|
| Azure Container Apps (API + worker) | $0 (within the monthly free grant) |
| Azure Static Web Apps | $0 (free plan) |
| Neon PostgreSQL | $0 (free tier, 0.5 GB) |
| GitHub (repo, Actions, container registry) | $0 |
| Claude API (chat + weekly research) | ≤ $5 (hard cap) |
| Domain name (optional) | ~$1 (≈ $10–15/year) |
| Apple Developer Program (with the iOS app) | $99/year |
| Google Play (with the Android app) | $25 once |

Risk: if the database outgrows Neon's free 0.5 GB (mainly the change history), the next tier is about $19/month — the history will be stored as compact diffs to avoid it.

## Release plan

1. **Web + API** — the first release (contents decided in stage 8).
2. **iOS app** (SwiftUI, with Sign in with Apple).
3. **Android app** (Kotlin, Jetpack Compose).

## Licensing and terms-of-service risks

| Topic | Risk | Plan |
|---|---|---|
| Game master (datamined) | Content belongs to Niantic / The Pokémon Company; shared "for educational use" | Fine for personal use; recheck before any public launch |
| Game sprites | Copyrighted artwork | Fine for personal use; recheck before going public |
| PvPoke data | MIT licence | Keep the attribution |
| Name and branding | "Pokémon" is a trademark | Use "MyPoGoCompanion", no logos, unofficial disclaimer |
| Reading the game / automation | Breaks Niantic's terms, risks bans | Never; the collection comes only from Poke Genie exports |
| Poke Genie export | The user's own data | Store per user; delete on request (GDPR) |
| AI web research | Copying news sites' content | Store facts with source links, not copied text |
| Google sign-in | Public apps need Google's consent-screen verification | Basic profile scopes only; verify if going public |
