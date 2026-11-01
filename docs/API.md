# The API contract (OpenAPI)

The API describes itself in an OpenAPI 3.1 document. That document is the **contract**: the web app's TypeScript client is generated from it, and the iOS and Android clients will be too.

| What | Where |
|---|---|
| The document, committed | `api/openapi.json` |
| The document, served | `/openapi/v1.json` on every copy (local, QA, prod) |
| Interactive docs page (Scalar) | `/docs` on **local and QA only** (`ApiDocs:Enabled`; decision 2026-10-31). Locally: http://localhost:5080/docs |
| How it's built | `api/MyPoGoCompanion.Api/OpenApi/`: title and description, the bearer scheme, which operations need a token, responses in order, string enums, and an example for every request and response type (`OpenApiExamples.cs`) |

## Changing the API

1. Change the endpoint. Describe it as you go: `.WithName("OperationId")` (it becomes the client function's name), `.WithSummary(...)`, `.ProducesProblem(status)` for every error it can answer, XML comments (`<summary>`, `<param>`) on request and response records (they become field descriptions).
2. Add an example for any new request or response type in `OpenApiExamples.cs`.
3. **Build the API in Debug** (`dotnet build`, or run it): the build rewrites `api/openapi.json`. Commit it with the change.
4. Add the new operation to `RealResponses_MatchTheDocument_ForEveryOperation` in `tests/MyPoGoCompanion.IntegrationTests/OpenApiTests.cs`, which fails until every operation is called.

Release builds (CI, the Docker image) don't touch the file; `dotnet build -c Release -p:UpdateOpenApi=true` writes it on purpose.

## What checks it

| Check | Fails when |
|---|---|
| `build-and-unit` → *Contract check — the committed OpenAPI document matches the API* | `api/openapi.json` isn't what the code produces (forgot to rebuild and commit) |
| `integration` → `OpenApiTests` | the served document isn't the committed one; an object type has no example; an operation that needs a token doesn't say so or doesn't list 401; a real answer (status, content type, required fields) isn't what the document says; an operation is never called; the docs page shows when switched off |
| Smoke test (QA and prod deploys) | `/openapi/v1.json` isn't served |

## Errors

Every error is a problem details body (`application/problem+json`, RFC 9457) with a `detail` written for people, also for 401, 403, 404 and 429 (the status-code pages add one when the endpoint sends none).
