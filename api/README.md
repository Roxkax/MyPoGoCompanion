# api/

The shared HTTP API used by the web app and, later, the mobile apps. ASP.NET Core (.NET 10) minimal APIs, EF Core with PostgreSQL, OpenAPI with generated clients.

Projects (`Core` arrives with the calculation engine):

| Folder | What it holds |
|---|---|
| `MyPoGoCompanion.Api/` | Endpoints, sign-in, OpenAPI, Dockerfile (deployed to Azure Container Apps `api-prod` / `api-qa`). Today: `/health`, `/health/live`, `/openapi/v1.json` |
| `MyPoGoCompanion.Core/` | Domain model, calculation engine (C# port of the keeper-filters pipeline), search-string simulator; shared with `jobs/` |
| `MyPoGoCompanion.Data/` | EF Core `AppDbContext`, entities by area (`GameData/`, `Rankings/`, `LiveData/`, `Users/`, `Operations/`), their table setup (`Model/`), migrations and the dev seed (`Seed/`). Tables and rules: `docs/DATABASE.md` |
| `*.UnitTests/` | xUnit v3 unit tests (Microsoft Testing Platform), one project next to each project above |

Integration tests against a real PostgreSQL live in `tests/`.

Package versions are set once in `Directory.Packages.props` at the repository root. How to run, configure and build the image: the root `README.md`.
