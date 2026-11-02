# The API contract (OpenAPI)

The API describes itself in an OpenAPI 3.1 document. That document is the **contract**: the web app's TypeScript client is generated from it, and the iOS and Android clients will be too.

| What | Where |
|---|---|
| The document, committed | `api/openapi.json` |
| The document, served | `/openapi/v1.json` on every copy (local, QA, prod) |
| Interactive docs page (**Swagger UI**) | `/docs` on **local and QA only** (`ApiDocs:Enabled`; decisions 2026-10-31 and 2026-11-01). Locally: http://localhost:5080/docs. every operation has *Try it out* on; for calls that need sign-in see below |
| How it's built | `api/MyPoGoCompanion.Api/OpenApi/`: title and description, the bearer scheme, which operations need a token, responses in order, string enums, and an example for every request and response type, plus one per error status (`OpenApiExamples.cs`) |

## Trying signed-in calls on the docs page

Swagger UI can't do the Google sign-in itself (Google sends people back to the web app, which swaps the code for our tokens). Instead, on QA or locally:

1. Sign in on the web app (same copy: QA web app for the QA API, local for local).
2. Profile menu → **Copy API token** (only on QA and local copies, never in production). It copies the current access token, refreshed first if it's nearly out.
3. On `/docs`, press **Authorize**, paste it and press *Authorize*. Calls with a lock now send it, for up to 15 minutes; copy a new one after that.

Don't use `POST /auth/refresh` on the docs page with the web app's refresh token: a refresh token works once, so the web app's copy would then count as reused and that sign-in ends.

## Changing the API

1. Change the endpoint. Describe it as you go: `.WithName("OperationId")` (the operation's name in the document, the docs page and the future mobile clients), `.WithSummary(...)`, `.ProducesProblem(status)` for every error it can answer, XML comments (`<summary>`, `<param>`) on request and response records (they become field descriptions).
2. Add an example for any new request or response type in `OpenApiExamples.cs`.
3. **Build the API in Debug** (`dotnet build`, or run it): the build rewrites `api/openapi.json`. Commit it with the change.
4. Add the new operation to `RealResponses_MatchTheDocument_ForEveryOperation` in `tests/MyPoGoCompanion.IntegrationTests/OpenApiTests.cs`, which fails until every operation is called.

Release builds (CI, the Docker image) don't touch the file; `dotnet build -c Release -p:UpdateOpenApi=true` writes it on purpose.

## The web app's client

Generated with **openapi-typescript** (types only) and called with **openapi-fetch**, with TanStack Query hooks from **openapi-react-query** (owner, 2026-11-01).

| What | Where |
|---|---|
| The types, generated and committed | `web/src/api/generated/schema.ts` (`npm run api:generate` in `web/`; not linted, formatted or counted for coverage) |
| The client | `web/src/api/client.ts`: `api.GET('/me')`, `api.PUT('/me/language', { body })`; in components `$api.useQuery('get', '/health')` and `$api.useMutation('put', '/me/language')`. Paths, parameters, bodies and answers are checked by the type check. `dataOrThrow()` turns a refusal into an `ApiError` with the problem's `detail` and its reference (the start of the problem's `traceId`, to find it in Application Insights: `docs/OBSERVABILITY.md`). `Schemas['MeResponse']` names a type |
| The access token | added by a middleware (`bearerToken`) while signed in, to every call except the four token endpoints (`/auth/google/client`, `/auth/google`, `/auth/refresh`, `/auth/sign-out`); a unit test checks this against the document's `security` |

After changing the API: build it (step 3 above), then `npm run api:generate` in `web/` and commit both files; a type error then shows every place in the web app the change affects.

`openapi-typescript` declares TypeScript 5 as a peer; `web/package.json` points it at the TypeScript 6 API package with an `overrides` entry (see `web/README.md`, "two versions side by side").

## What checks it

| Check | Fails when |
|---|---|
| `build-and-unit` → *Contract check — the committed OpenAPI document matches the API* | `api/openapi.json` isn't what the code produces (forgot to rebuild and commit) |
| `build-and-unit` → *Contract check — the generated API client matches the OpenAPI document* | `web/src/api/generated/` isn't what `api/openapi.json` generates (forgot `npm run api:generate`) |
| `build-and-unit` → web type check | the web app calls the API in a way the document doesn't allow (wrong path, parameter, body or answer field) |
| `integration` → `OpenApiTests` | the served document isn't the committed one; an object type has no example; an operation that needs a token doesn't say so or doesn't list 401; a real answer (status, content type, required fields) isn't what the document says; an operation is never called; the docs page shows when switched off |
| Smoke test (QA and prod deploys) | `/openapi/v1.json` isn't served |

## Errors

Every error is a problem details body (`application/problem+json`, RFC 9457) with a `detail` written for people, also for 401, 403, 404 and 429 (the status-code pages add one when the endpoint sends none).
