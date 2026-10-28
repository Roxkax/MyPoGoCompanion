# MyPoGoCompanion

A free companion for Pokémon GO players: a shared API, a web app and, later, native mobile apps. Unofficial fan project, not affiliated with Niantic, Nintendo, Creatures or Game Freak, or The Pokémon Company.

## Repository layout

| Folder | What it holds |
|---|---|
| `api/` | ASP.NET Core (.NET 10) API, domain/calculation engine and EF Core data layer, with their unit tests |
| `jobs/` | Scheduled Azure Container Apps Jobs (daily import, weekly AI researcher, backups) |
| `web/` | React + TypeScript web app (Vite, installable PWA) |
| `tests/` | Integration tests (Testcontainers), UI tests (Playwright) and golden files |
| `design/` | App map, screen designs and their sources |
| `planning/` | Plan, specs, priorities, backlog and CI/CD decisions |
| `infra/` | Azure template (`main.bicep`) for prod and QA; setup steps in `docs/SETUP-ENVIRONMENTS.md` |
| `docs/` | Environment setup, planning process and the keeper-filters methodology |
| `legacy/keeper-filters/` | The original Python pipeline and its single-page output. Kept until the app's filters replace it (see `docs/KEEPER-FILTERS.md`) |
| `.github/` | GitHub Actions workflows |

Later: `ios/` (SwiftUI) and `android/` (Kotlin, Jetpack Compose).

## Develop locally

### What you need

| Tool | Version | Used for |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 (pinned in `global.json`) | API, jobs, .NET tests |
| [Node.js](https://nodejs.org) | 24 LTS (`.nvmrc`; 22.12+ works) | web app, UI tests |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | any recent | local PostgreSQL, integration tests (Testcontainers) |
| Python | 3.11+ | only for `legacy/keeper-filters/` |

Editors pick up `.editorconfig` automatically (Visual Studio, Rider and VS Code with the C# Dev Kit and ESLint/Prettier extensions).

### First time

```bash
git clone https://github.com/Roxkax/PoGoCompanion.git
cd PoGoCompanion
cd web && npm ci && cd ..
```

### Local database

PostgreSQL 17 (the same major version as Neon) runs in Docker. Defaults: `localhost:5432`, database, user `mypogocompanion`, password `localdev`. To change the port or password, copy `.env.example` to `.env`.

```bash
docker compose up -d --wait          # start (waits until it's ready)
docker compose down                  # stop, keep the data
docker compose down -v               # stop and wipe the data
docker compose exec db psql -U mypogocompanion   # SQL prompt
```

Connection string for the API: `Host=localhost;Port=5432;Database=mypogocompanion;Username=mypogocompanion;Password=localdev`.

The tables come from EF Core migrations and the dev seed (the design's sample trainer and data). Both arrive with the database schema (E01-F01); after that the API applies them on start-up in development.

### Formatting and lint

```bash
dotnet format MyPoGoCompanion.slnx --verify-no-changes   # check .NET code (drop the flag to fix)
cd web && npm run lint && npm run format:check           # check web code
cd web && npm run lint:fix && npm run format             # fix web code
```

The .NET build turns every analyzer warning into an error when `CI=true` (as in GitHub Actions); locally they stay warnings. Run `CI=true dotnet build` to see what CI will reject.

### API, web app, jobs and tests

Added in Sprint 0 as each piece lands: the API and web skeletons (E00-F03), unit tests (E00-F04), integration tests (E00-F05) and UI tests (E00-F07).

### Legacy keeper-filters page

```bash
cd legacy/keeper-filters
pip install -r requirements.txt
python build.py            # fetch data, rebuild every string, write dist/pvp-keeper-filters.html
```

See `docs/KEEPER-FILTERS.md`.
