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
| `docs/` | Environment setup, prod deploy, branch rules, planning process and the keeper-filters methodology |
| `legacy/keeper-filters/` | The original Python pipeline and its single-page output. Kept until the app's filters replace it (see `docs/KEEPER-FILTERS.md`) |
| `.github/` | GitHub Actions workflows, the `main` ruleset, Dependabot and the PR template (`docs/BRANCH-RULES.md`) |

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
git clone https://github.com/Roxkax/MyPoGoCompanion.git
cd MyPoGoCompanion
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

The tables come from EF Core migrations (`api/MyPoGoCompanion.Data/Migrations/`) and the dev seed (the design's sample trainer and data). Today there is only an empty `Baseline` migration; the schema and the seed arrive with E01-F01, and after that the API applies them on start-up in development.

```bash
dotnet tool restore                                                    # once: installs dotnet-ef
dotnet ef migrations add <Name> --project api/MyPoGoCompanion.Data     # after changing the model
dotnet ef database update --project api/MyPoGoCompanion.Data           # apply to the local database
```

`dotnet ef database update` uses `ConnectionStrings__Database` when it is set, and the local database otherwise. A model change without a migration fails the `integration` check.

### Formatting and lint

```bash
dotnet format MyPoGoCompanion.slnx --verify-no-changes   # check .NET code (drop the flag to fix)
cd web && npm run lint && npm run format:check           # check web code
cd web && npm run lint:fix && npm run format             # fix web code
```

The .NET build turns every analyzer warning into an error when `CI=true` (as in GitHub Actions); locally they stay warnings. Run `CI=true dotnet build` to see what CI will reject.

### API

```bash
dotnet run --project api/MyPoGoCompanion.Api        # http://localhost:5080 (needs the local database)
```

| Address | What it is |
|---|---|
| `/health` | `{"status":"Healthy","version":"dev","checks":{"database":"Healthy"}}`; HTTP 503 when the database can't be reached |
| `/health/live` | The process only, without touching the database (for container probes) |
| `/openapi/v1.json` | OpenAPI description (the generated clients come from it) |

Settings (environment variables in Azure, `appsettings.Development.json` locally):

| Setting | Example | Notes |
|---|---|---|
| `ConnectionStrings__Database` | `postgresql://user:pass@host/db?sslmode=require` | Neon's URL or Npgsql's `Host=…;Database=…` form |
| `Cors__AllowedOrigins` | `https://….azurestaticapps.net` | Comma-separated web app addresses allowed to call the API |
| `APP_VERSION` | Git commit | Shown by `/health`; the Docker image sets it from the `VERSION` build argument |

Docker image (build from the repository root; listens on port 8080):

```bash
docker build -f api/MyPoGoCompanion.Api/Dockerfile --build-arg VERSION=$(git rev-parse --short HEAD) -t mypogocompanion-api .
```

### Web app

```bash
cd web
npm run dev          # http://localhost:5173, calls the API at VITE_API_URL (web/.env.development)
npm run build        # type check + production build in web/dist
```

`VITE_API_URL` is the API's address, fixed at build time (the deploy pipelines set it for QA and prod).

### Tests

```bash
dotnet test --solution MyPoGoCompanion.slnx                  # .NET unit + integration tests (integration needs Docker)
dotnet test --project api/MyPoGoCompanion.Api.UnitTests       # .NET unit tests only
cd web && npm test                                            # web unit tests (Vitest)
cd web && npm run test:coverage                               # web unit tests with coverage
cd tests/ui && npm ci && npx playwright install chromium && npx playwright test   # UI tests
```

- **Integration tests** start their own PostgreSQL in Docker (Testcontainers); the local database isn't used. The migrations are applied once per run to a template database; `PostgresFixture.ConnectionString` is a shared copy for tests that only read, and `CreateDatabaseAsync()` gives a test that writes a fresh copy of its own. Run them alone with `dotnet test --project tests/MyPoGoCompanion.IntegrationTests`.
- **UI tests** run on phone and desktop sizes in light and dark. Without `BASE_URL` they start the API and the web dev server themselves (the local database must be running); `BASE_URL=https://… npx playwright test` runs them against a deployed web app. A failed test keeps a trace, a screenshot and a video; `npm run report` opens them.
- **QA deploy** (workflow "QA deploy", check `deploy-qa`): opt-in. Add the **`qa` label** to a pull request to deploy it to the shared QA environment (and run the UI tests); while the label is on, every push deploys again (pushes that only change notes skip it). The links are commented on the PR; QA shows one PR at a time (`planning/CI-CD.md`). **Actions → QA deploy → Run workflow** on a branch deploys it once, label or not.
- **Prod deploy** (workflow "Prod deploy"): every merge to `main` that changes more than notes goes to prod (migrations → API → web app → smoke test), then the QA database is reset to a copy of prod. A failure opens an issue "Prod deploy failed" assigned to the owner. Rollback and failure handling: `docs/PROD-DEPLOY.md`.
- **Smoke test** (`tests/smoke/smoke.sh`, workflow "Smoke test"): quick checks after every deploy that the API is healthy, runs the expected version, allows the web app's address and that the web app loads. Run it by hand with `API_URL=… WEB_URL=… tests/smoke/smoke.sh`.

### Code coverage

Unit tests must cover at least **90% of lines**, separately for .NET and for the web app; the `build-and-unit` check fails below that and posts the coverage table on the pull request. Generated code and `Program.cs` (startup wiring, exercised by the integration tests) don't count (`coverage.config`).

```bash
# .NET: coverage report in coverage/dotnet (open index.html)
dotnet tool restore
dotnet test --project api/MyPoGoCompanion.Api.UnitTests --coverage --coverage-settings coverage.config --coverage-output unit.cobertura.xml --results-directory "$PWD/TestResults/unit"
dotnet reportgenerator -reports:'TestResults/unit/**/*.cobertura.xml' -targetdir:coverage/dotnet -reporttypes:'Html;JsonSummary'

# Web: table in the terminal; fails below the minimum
cd web && npm run test:coverage
```

### What the `build-and-unit` check runs

`.github/workflows/build-and-unit.yml`, on every pull request and on `main`:

| Part | Steps |
|---|---|
| .NET | restore → `dotnet format --verify-no-changes` → build in Release (warnings fail it) → every `*.UnitTests` project with coverage → coverage minimum |
| Web | `npm ci` → lint → Prettier check → type check → Vitest with coverage (minimum in `web/vite.config.ts`) → production build |
| `build-and-unit` | Passes only when both parts pass; writes the coverage table to the run summary and as a pull-request comment |

Jobs are added with the first scheduled job (E02-F01).

### What the `integration` check runs

`.github/workflows/integration.yml`, on every pull request and on `main`: build the integration tests in Release → run them against PostgreSQL 17 in Docker (migrations applied first) → test results (totals, each failure with its error, all tests) on the run summary and as a pull-request comment; the TRX file is kept as a run artifact for 7 days.

### What the `ui-tests` check runs

Inside the QA deploy (`.github/workflows/deploy-qa.yml`), right after the smoke test and while that run still holds the QA queue, so no other PR replaces QA half-way through: the Playwright tests in `tests/ui` against the QA web app, on phone (393×852) and desktop (1440) sizes in light and dark, one retry per test. The results (totals, each failure with its error) go to the run summary and into the "QA now shows PR #n" comment; the HTML report, with a trace, screenshot and video for every failed test, is kept as the run artifact **ui-test-report** for 14 days (unzip and open `index.html`, or drop a `trace.zip` on https://trace.playwright.dev). The job `ui-tests` turns that result into its own check: red when a test fails or the deploy stopped before the tests; skipped (and so passed) when the deploy was skipped (no `qa` label, or only notes changed).

### Legacy keeper-filters page

```bash
cd legacy/keeper-filters
pip install -r requirements.txt
python build.py            # fetch data, rebuild every string, write dist/pvp-keeper-filters.html
```

See `docs/KEEPER-FILTERS.md`.
