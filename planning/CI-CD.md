# MyPoGoCompanion — CI/CD and GitHub setup

Decided with the owner on 2026-10-28. Built as **Sprint 0 "Delivery pipeline"**, the first item of the backlog; the pipelines start doing real work as soon as the API and web app exist.

## Environments

| | Web app | API | Database |
|---|---|---|---|
| **Prod** | Azure Static Web Apps (production) | Azure Container Apps `api-prod` (scale to zero) | Neon `main` branch |
| **QA** (one, shared) | Azure Static Web Apps named environment `qa` (fixed address) | Azure Container Apps `api-qa` (scale to zero) | Neon branch `qa`, **reset to a fresh copy of prod after every prod deploy** |

- **Prod deploys automatically** when a PR is merged and `main` is updated: build → database migrations → API → web.
- **QA shows the most recently updated open PR.** Every push to an open PR deploys it to QA; the PR gets a comment "QA now shows PR #n". QA deploy + UI tests run in one queued job (one PR at a time), so two PRs never overwrite each other mid-test. A "Deploy this PR to QA" re-run brings an older PR back.
- Scheduled jobs (daily import, weekly AI researcher, backups) run only in prod; they can be started by hand against QA.

## Pipelines (required checks on every PR)

| Check | What it runs |
|---|---|
| **build-and-unit** | .NET build + xUnit unit tests; web lint, format, type check + Vitest; golden-file tests of the keeper filters (from the Python build) |
| **integration** | API against a real PostgreSQL started in the pipeline (Testcontainers): endpoints, EF migrations, Poke Genie import, search-string simulator; fixed test data, exact numbers |
| **deploy-qa** | Deploys the PR to the shared QA environment |
| **ui-tests** | Playwright end-to-end on phone and desktop sizes against QA (real prod-copy data, so tests check behaviour rather than fixed values) |

## Merge rules on `main`

Switched on at **Sprint 0**, together with the pipelines (until then planning documents are pushed straight to `main`).


- No direct pushes; changes only through pull requests; no force pushes or deletion.
- All four checks must pass; the branch must be up to date with `main`; all PR conversations resolved.
- **No approval required while the owner works alone** (GitHub doesn't allow approving your own PR, and Claude's commits are authored by the owner). When collaborators join: **1 approval required** + `CODEOWNERS` making the owner approve changes to `.github/`, infrastructure and pipeline files.

## Secrets and access

- GitHub → Azure sign-in with **OIDC** (federated credentials, no stored passwords).
- GitHub **environments** `prod` and `qa` hold their own secrets: Neon connection string, Google OAuth client, Anthropic API key, search API key. The repository is public, so nothing secret is ever committed.
- Static Web Apps deployment token per environment.

## Costs

- Repository is public: GitHub Actions minutes and branch protection are free.
- Azure Static Web Apps free plan (production + named environments), Container Apps scale to zero (QA idles at ~€0), Neon free plan (branches included). No new running cost.

## What the owner sets up by hand (steps given at Sprint 0)

1. Azure: resource group, Static Web App, Container Apps environment with `api-prod` and `api-qa`, container registry, federated credential for GitHub.
2. Neon: project with `main` and `qa` branches.
3. GitHub: environments `prod` and `qa` with their secrets; the `main` branch rules above (Settings → Rules).
