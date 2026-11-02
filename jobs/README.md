# jobs/

Scheduled work, run as Azure Container Apps Jobs (scale to zero, separate from the API): the daily game-data import, the weekly AI researcher, backups and reminders. Each job is a small .NET console app that reuses `api/MyPoGoCompanion.Core` and `api/MyPoGoCompanion.Data`, with its unit tests in a `*.UnitTests/` project next to it. Telemetry comes from `api/MyPoGoCompanion.Observability`: call `builder.AddTelemetry("<job name>")` on the host builder (`docs/OBSERVABILITY.md`); the job's Container Apps Job needs the copy's `APPLICATIONINSIGHTS_CONNECTION_STRING`, as the API gets it in `.github/scripts/deploy_api.sh`.

Jobs run on their schedule only in prod; they can be started by hand against QA.
