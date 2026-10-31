# MyPoGoCompanion — CI/CD and GitHub setup

Decided with the owner on 2026-10-28. Built as **Sprint 0 "Delivery pipeline"**, the first item of the backlog; the pipelines start doing real work as soon as the API and web app exist.

## Environments

| | Web app | API | Database |
|---|---|---|---|
| **Prod** | Azure Static Web Apps (production) | Azure Container Apps `api-prod` (scale to zero) | Neon `main` branch |
| **QA** (one, shared) | Azure Static Web Apps named environment `qa` (fixed address) | Azure Container Apps `api-qa` (scale to zero) | Neon branch `qa`, **reset to a fresh copy of prod after every prod deploy** |

- **Prod deploys automatically** when a PR is merged and `main` is updated: build → database migrations → API → web.
- **QA deploys are opt-in with the `qa` label** (owner, 2026-10-31; replaces "every push deploys"). Adding the label to a PR deploys it to QA and runs the UI tests; while the label is on, every push deploys again; removing it stops that. PRs without the label don't wait for QA: `deploy-qa` and `ui-tests` are skipped, which branch rules count as passed, so QA testing before a merge is the owner's choice. The prod smoke test (E00-F08) still checks every merge.
- **QA shows the most recently deployed PR.** The PR gets a comment "QA now shows PR #n". QA deploy + UI tests run in one queued job (one PR at a time), so two PRs never overwrite each other mid-test. Removing and adding the `qa` label again (or re-running the check) brings an older PR back.
  - As built (E00-F06, `.github/workflows/deploy-qa.yml`): API image to GitHub Container Registry (tagged with the commit) → migrations on the Neon `qa` branch (EF Core migration bundle) → web app to the Static Web Apps environment `qa` → API to `api-qa` (database URL as a Container Apps secret, the QA web address allowed by CORS) → smoke test → PR comment with the links. Other open PRs' comments change to "QA has moved on to PR #n".
  - **Notes-only pushes skip the deploy**: when everything changed since what QA shows for the PR (or since the PR's base) is Markdown, `docs/`, `planning/` or `design/`, the `deploy-qa` job is skipped, which branch rules count as passed. Running the workflow by hand always deploys.
  - **PRs from forks aren't deployed** (they get no secrets). Drafts follow the label like any other PR. Adding a label other than `qa` doesn't redeploy.
  - The queue is GitHub's concurrency group `qa-environment`: a running deploy is never cancelled; GitHub keeps one run waiting and cancels an older waiting run when a newer push arrives (QA would show the newer PR anyway). Remove and add the `qa` label, re-run the check, or **Actions → QA deploy → Run workflow** on the PR's branch (always deploys, label or not) to bring a PR back.
- Scheduled jobs (daily import, weekly AI researcher, backups) run only in prod; they can be started by hand against QA.

## Pipelines (required checks on every PR)

| Check | What it runs |
|---|---|
| **build-and-unit** | .NET build + xUnit unit tests; web lint, format, type check + Vitest; golden-file tests of the keeper filters (from the Python build) |
| **integration** | API against a real PostgreSQL started in the pipeline (Testcontainers): endpoints, EF migrations, Poke Genie import, search-string simulator; fixed test data, exact numbers |
| **deploy-qa** | Deploys the PR to the shared QA environment (only PRs with the `qa` label; skipped, so passed, otherwise) |
| **ui-tests** | Playwright end-to-end on phone and desktop sizes against QA (real prod-copy data, so tests check behaviour rather than fixed values); runs with the QA deploy |

## Merge rules on `main`

Switched on at **Sprint 0**, together with the pipelines (until then planning documents are pushed straight to `main`).


- No direct pushes; changes only through pull requests; no force pushes or deletion.
- All four checks must pass (`deploy-qa` and `ui-tests` pass as skipped on PRs without the `qa` label); the branch must be up to date with `main`; all PR conversations resolved.
- **No approval required while the owner works alone** (GitHub doesn't allow approving your own PR, and Claude's commits are authored by the owner). When collaborators join: **1 approval required** + `CODEOWNERS` making the owner approve changes to `.github/`, infrastructure and pipeline files.

## Secrets and access

- GitHub → Azure sign-in with **OIDC** (federated credentials, no stored passwords).
- GitHub **environments** `prod` and `qa` hold their own secrets: Neon connection string, Google OAuth client, Anthropic API key, search API key. The repository is public, so nothing secret is ever committed.
- Static Web Apps deployment token per environment.

## Addresses

Azure's free default addresses for prod and QA for now (`*.azurestaticapps.net`, `*.azurecontainerapps.io`). A custom domain can be added later (~€10–20/year, free HTTPS); avoid trademarked words in it if the app goes public.

## Costs

- Repository is public: GitHub Actions minutes and branch protection are free.
- Azure Static Web Apps free plan (production + named environments), Container Apps scale to zero (QA idles at ~€0), Neon free plan (branches included). No new running cost.

## What the owner sets up by hand (steps given at Sprint 0)

1. Azure: resource group, Static Web App, Container Apps environment with `api-prod` and `api-qa`, container registry, federated credential for GitHub.
2. Neon: project with `main` and `qa` branches.
3. GitHub: environments `prod` and `qa` with their secrets; the `main` branch rules above (Settings → Rules).
