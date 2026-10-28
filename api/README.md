# api/

The shared HTTP API used by the web app and, later, the mobile apps. ASP.NET Core (.NET 10) minimal APIs, EF Core with PostgreSQL, OpenAPI with generated clients.

Planned projects (created from Sprint 0 onwards):

| Folder | What it holds |
|---|---|
| `MyPoGoCompanion.Api/` | Endpoints, sign-in, OpenAPI, Dockerfile (deployed to Azure Container Apps `api-prod` / `api-qa`) |
| `MyPoGoCompanion.Core/` | Domain model, calculation engine (C# port of the keeper-filters pipeline), search-string simulator; shared with `jobs/` |
| `MyPoGoCompanion.Data/` | EF Core `DbContext`, entities and migrations |
| `*.UnitTests/` | xUnit unit tests, one project next to each project above |

Integration tests against a real PostgreSQL live in `tests/`.
