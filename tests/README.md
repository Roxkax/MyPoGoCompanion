# tests/

Tests that span more than one project. Unit tests live next to the code they test (`*.UnitTests/` in .NET, `*.test.ts(x)` in the web app).

| Folder | Check | What it holds |
|---|---|---|
| `MyPoGoCompanion.IntegrationTests/` | `integration` | The API against a real PostgreSQL started with Testcontainers: endpoints, EF migrations, Poke Genie import, search-string simulator. Fixed test data, exact numbers. |
| `ui/` | `ui-tests` | Playwright end-to-end tests on phone (393×852) and desktop (1440) sizes, light and dark, run against the QA environment. |
| `golden/` | `build-and-unit` | Outputs frozen from the Python keeper-filters build (`legacy/keeper-filters/`), compared exactly against the C# port. |

See `planning/CI-CD.md` for when each check runs.
