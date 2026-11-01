# tests/

Tests that span more than one project. Unit tests live next to the code they test (`*.UnitTests/` in .NET, `*.test.ts(x)` in the web app).

| Folder | Check | What it holds |
|---|---|---|
| `MyPoGoCompanion.IntegrationTests/` | `integration` | The API against a real PostgreSQL started with Testcontainers: endpoints, EF migrations (applied once per run; each writing test gets its own fresh copy), Poke Genie import, search-string simulator. Fixed test data, exact numbers. |
| `ui/` | `ui-tests` | Playwright end-to-end tests on phone (393×852) and desktop (1440) sizes, light and dark, run against the QA environment at the end of every QA deploy (PRs with the `qa` label) (or locally against the API and web dev server). `specs/journey.spec.ts` is the smoke journey that grows with the app. Failed tests keep a trace, screenshot and video (run artifact `ui-test-report`). |
| `smoke/` | (after every deploy) | `smoke.sh`: quick checks that a deployed API and web app work together (health, version, CORS, pages). Run at the end of every QA deploy (`deploy-qa`), and by hand with the "Smoke test" workflow. |
| `golden/` | `build-and-unit` | Outputs frozen from the Python keeper-filters build (`legacy/keeper-filters/`) with the inputs they were built from, compared exactly against the C# port (`golden/keeper-filters/README.md`). |
| `MyPoGoCompanion.GoldenFiles/` | (used by tests) | The golden-file harness: `GoldenDirectory.KeeperFilters` reads the frozen files, `GoldenAssert.Matches` compares C# output with one and fails with a readable diff. Its own tests, and checks that the frozen files are complete and consistent, are in `MyPoGoCompanion.GoldenFiles.UnitTests/`. |

See `planning/CI-CD.md` for when each check runs.
