# jobs/

Scheduled work, run as Azure Container Apps Jobs (scale to zero, separate from the API): the daily game-data import now; the weekly AI researcher, backups and reminders later.

| Project | What it is |
|---|---|
| `MyPoGoCompanion.Jobs` | One .NET console app and one image (`Dockerfile`) for every job; the first argument names the job. `daily-import` is the daily import: `docs/DAILY-IMPORT.md` |
| `MyPoGoCompanion.Jobs.UnitTests` | Its unit tests, with trimmed real source files in `Fixtures/` |

The jobs reuse `api/MyPoGoCompanion.Data` (the database and its connection string, `DatabaseConnectionString`), `api/MyPoGoCompanion.Engine` (the calculations, `docs/ENGINE.md`) and `api/MyPoGoCompanion.Observability` (`builder.AddTelemetry("<job name>")`, `docs/OBSERVABILITY.md`). The deploy pipelines build the image `ghcr.io/roxkax/mypogocompanion-jobs:<commit>` and create or update each Container Apps Job with its settings (`.github/scripts/deploy_job.sh`: the database secret, the copy's Application Insights). Jobs run on their schedule only in prod; on QA they are started by the QA deploy and by hand (**Actions → Run daily import**).

Run one locally:

```bash
ConnectionStrings__Database="Host=localhost;Database=mypogocompanion;Username=mypogocompanion;Password=localdev" dotnet run --project jobs/MyPoGoCompanion.Jobs -- daily-import
```
