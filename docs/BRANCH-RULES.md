# Branch rules, dependency updates and code scanning

How `main` is protected, how to switch the rules on, and what to do when a pull request can't merge. The decisions behind it are in `planning/CI-CD.md` ("Merge rules on `main`").

## What the rules enforce

The ruleset **Protect main** is kept as code in `.github/rulesets/main.json` and switched on in **Settings → Rules → Rulesets**.

| Rule | What it means |
|---|---|
| Changes only through pull requests | Nobody pushes to `main` directly, the owner included (planning documents too) |
| Required checks pass | `build-and-unit`, `integration`, `deploy-qa`, `ui-tests` (from GitHub Actions only). `deploy-qa` and `ui-tests` are skipped, and so pass, on pull requests without the `qa` label |
| Branch up to date before merging | The checks must have run on the pull request merged with the latest `main`. When `main` moves on, press **Update branch** and the checks run again |
| Conversations resolved | Every review comment thread on the pull request is resolved |
| Approvals required: 0 | The owner works alone and GitHub doesn't let you approve your own pull request. When collaborators join: 1 approval and a `CODEOWNERS` file for `.github/`, `infra/` and pipeline files |
| No force pushes, no deletion | `main`'s history can't be rewritten and the branch can't be deleted |

Nobody is on the bypass list. In a real emergency the owner (repository admin) can set the ruleset's enforcement to **Disabled**, push the fix, and set it back to **Active** straight away; the weekly **Branch rules check** fails while it's off.

Merging with a merge commit, squash or rebase is allowed (as today).

## Switching the rules on (once)

1. Open `.github/rulesets/main.json` on GitHub (on `main`, or on the pull request's branch before the merge) and press **Download raw file**.
2. **Settings → Rules → Rulesets → New ruleset ▾ → Import a ruleset**, pick the file, check the page shows the rules above, and press **Create**.
3. **Actions → Branch rules check → Run workflow** (on `main`). Its summary lists each rule as GitHub enforces it; green means it matches the file.

To change a rule later: edit the file in a pull request, and after the merge edit the ruleset in Settings the same way (or delete it and import the file again). The **Branch rules check** runs when the file changes on `main` and fails until GitHub matches it again. It also runs every Monday.

## When a pull request can't merge

| What GitHub shows | What to do |
|---|---|
| **This branch is out-of-date with the base branch** | Press **Update branch**; the checks run again |
| A check **failed** | Open it from the checks list and fix the code (red `build-and-unit`: lint, format, tests or coverage below 90%; red `integration`: integration tests; red `deploy-qa` / `ui-tests`: the QA deploy or the UI tests on QA, see the run's summary and the `ui-test-report` artifact) |
| `deploy-qa` **failed** for a reason outside the code (Azure or Neon hiccup) | Re-run the failed jobs. If you no longer want the PR on QA, remove the `qa` label and push again: the new run skips the QA checks |
| `deploy-qa` **cancelled** | Another PR took QA while this one was waiting. Re-run the workflow, or remove and add the `qa` label |
| **Expected — Waiting for status to be reported** | The workflow didn't start (for example GitHub Actions had an outage). Push again or close and reopen the pull request |
| **Unresolved conversations** | Resolve each review thread (answer it or press **Resolve conversation**) |

Adding a label other than `qa` to a PR that has it starts a run that skips the QA checks; that newer, skipped result is the one GitHub counts. If a QA deploy had failed, re-run that failed run instead of relying on the newer one.

## Dependabot

`.github/dependabot.yml` checks for updates every **Monday at 07:00 (Lisbon)**:

| Ecosystem | Where | Pull requests |
|---|---|---|
| NuGet | `Directory.Packages.props`, `.config/dotnet-tools.json` | Minor and patch updates grouped in one PR; each major update on its own. The .NET framework's packages (ASP.NET Core, EF Core, `Microsoft.Extensions.*`), Npgsql and `dotnet-ef` don't get major updates: the app stays on .NET 10 LTS until the SDK in `global.json` is moved by hand |
| npm | `web/`, `tests/ui/` | Minor and patch grouped per folder; majors on their own |
| GitHub Actions | `.github/workflows/` | Minor and patch grouped; majors on their own |

At most 5 open pull requests per ecosystem. Each one runs the required checks like any other PR; merge it to take the update, or close it to skip that version (Dependabot then waits for the next one). Dependabot's runs get a read-only token and no secrets, so its PRs get no coverage or test-result comments (the summaries are on the run pages) and are never deployed to QA by Dependabot's own pushes. To try one on QA: **Actions → QA deploy → Run workflow** on its branch.

Security alerts for vulnerable dependencies: **Settings → Advanced Security → Dependabot alerts** and **Dependabot security updates** (both free; switch them on once).

## CodeQL code scanning

`.github/workflows/codeql.yml` scans the C# API, the TypeScript web app and UI tests, the Python scripts and the workflows on every pull request, on `main` and every Monday. Results appear under **Security → Code scanning** and as annotations on the pull request. It is **not** a required check; a new alert is something to fix in the same pull request or open an issue for.

Leave CodeQL's **default setup** off (Settings → Advanced Security → Code scanning): while it's on, GitHub rejects this workflow's results.
