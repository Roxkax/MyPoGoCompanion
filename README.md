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
| `docs/` | Planning process and the keeper-filters methodology |
| `legacy/keeper-filters/` | The original Python pipeline and its single-page output. Kept until the app's filters replace it (see `docs/KEEPER-FILTERS.md`) |
| `.github/` | GitHub Actions workflows |

Later: `ios/` (SwiftUI) and `android/` (Kotlin, Jetpack Compose).

How to run everything locally is added during Sprint 0.
