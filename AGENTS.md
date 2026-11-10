# MyPoGoCompanion — Codex instructions

## Start here

- Read [project context](docs/PROJECT-CONTEXT.md), especially **Sprint progress**, **Owner to-dos**, the relevant **Decision log** entries and **Open decisions**, before planning work. It holds the full history; this file holds the current working agreements.
- Read [README.md](README.md) for the repository layout and local workflow, and the area-specific guide before changing that area. Read [docs/KEEPER-FILTERS.md](docs/KEEPER-FILTERS.md) before changing the legacy Python pipeline.
- Work follows [planning/BACKLOG.md](planning/BACKLOG.md), GitHub issues and the project board. Start a new sprint only when the owner says so. A direct request for maintenance or setup authorizes that work without starting a sprint.
- Use the existing checkout in Codex Cloud. Each task is isolated; do not create a Git worktree unless the owner requests one. Preserve unrelated changes.
- Codex is the development assistant. The planned in-app chat and weekly researcher use the **OpenAI API** behind the provider-agnostic .NET interface; do not use the Codex development session as the app's backend. The application enforces its adjustable **$5/month** AI cap. Provider integration and credentials are still future feature work.

## Working agreements

- **Questions:** ask one question at a time, in plain chat, so the owner can answer freely. Give options and a recommendation when useful. Resolve routine implementation details within the approved scope without re-asking for permission.
- **Product decisions:** the owner describes features from the user's point of view; Codex works out the backend and confirms new product or architectural choices before treating them as decided.
- **Feasibility:** say what the data or game allows and offer realistic alternatives. There is no official game API; collection imports use Poke Genie exports.
- **Scope:** the product is free. Every feature is in scope; anything "premium" is only noted for a possible later commercial phase.
- **Cost:** prefer free tiers, pay-per-use and scale-to-zero services. Flag features with running costs.
- **Commercial readiness:** design for many users and flag relevant licensing, terms-of-service and trademark issues with Pokémon/Niantic data, sprites and community sources.
- **Language:** replies in English; the owner may write in Portuguese.
- **Decision log:** record each confirmed decision immediately, with its date, in [docs/PROJECT-CONTEXT.md](docs/PROJECT-CONTEXT.md#decision-log). Preserve historical facts and add dated entries when a decision supersedes an earlier one.
- **Progress:** update **Sprint progress** in that document after each task, PR, merge or setup step (status, PR, remaining work, owner to-dos), and commit it with the work.
- **Task naming:** use `<feature key> <feature name>` for a Codex task when naming is available; use a short descriptive name for other work.

## Git and pull requests

- Use a descriptive `codex/` branch for new work. All changes, including planning documents, reach `main` through a PR. Do not push directly to `main` or merge without the owner's instruction.
- Commit and push planning documents after confirmed changes. Author commits as `Diogo Gonçalves <3799595+Roxkax@users.noreply.github.com>`; commits written by Codex use `Co-Authored-By: Codex <noreply@openai.com>`. Do not rewrite existing attribution or rename historical branch references.
- A feature PR closes every issue of that feature, including the parent and its **Owner review & testing** task, with a separate `Closes #n` for each. Do not invent issue numbers for maintenance work. Merging is the owner's approval.
- Before creating a PR, ask whether it should get the `qa` label unless the owner already specified the choice. Adding it deploys to shared QA and runs UI tests. Keep the existing opt-in policy.
- Rebase the branch on the latest `main` before review; do not introduce merge commits into the branch. Follow [docs/BRANCH-RULES.md](docs/BRANCH-RULES.md) and [planning/CI-CD.md](planning/CI-CD.md).
- After opening a PR, monitor CI, any requested QA deployment and reviews during the task; fix failures and report the actual outcome. If access prevents monitoring, report the blocker rather than claiming checks passed.
- Use [.github/pull_request_template.md](.github/pull_request_template.md). Describe the behavior changed and the checks actually run.

## Development and validation

The toolchain is .NET 10 (`global.json`), Node 24 (`.nvmrc`) and PostgreSQL 17 (`compose.yaml`). Python 3.11+ is only needed for the legacy keeper filters. Use `npm ci` with the checked-in lockfiles and `dotnet tool restore` for local .NET tools.

In the prepared Codex Cloud environment, source `/workspace/setup/env.sh` in each shell if it exists. It selects the installed .NET SDK, writable runtime/cache directories, system Chromium and FFmpeg. Follow the saved environment startup instructions for services; live processes do not survive a snapshot. Keep these machine-specific paths out of application configuration.

From the repository root:

```bash
dotnet restore MyPoGoCompanion.slnx
dotnet tool restore
CI=true dotnet build MyPoGoCompanion.slnx --configuration Release --no-restore
dotnet format MyPoGoCompanion.slnx --verify-no-changes --no-restore
dotnet test --project api/MyPoGoCompanion.Api.UnitTests --configuration Release --no-build
dotnet test --project tests/MyPoGoCompanion.IntegrationTests --configuration Release --no-build
```

Choose the relevant `*.UnitTests` projects for the changed code; integration tests require Docker and start their own database. CI runs every unit project and enforces at least 90% line coverage separately for .NET and web. The commands and coverage workflow are in [README.md](README.md#tests).

From `web/`: `npm ci`, `npm run lint`, `npm run format:check`, `npm run test:coverage`, `npm run build`. From `tests/ui/`: `npm ci`, `npm run typecheck`, and the relevant Playwright tests.

For local UI work, start the database with `docker compose up -d --wait`, run the API with the Development launch profile (`--launch-profile http`), and start the web dev server. Verify `/health`, populated `/pokedex` data and `tests/smoke/smoke.sh`; an open port alone does not establish readiness. Google sign-in credentials are optional for the seeded local workflow.

- API Debug builds regenerate tracked `api/openapi.json`. Use Release for setup or unrelated changes; deliberately regenerate the OpenAPI document and TypeScript client when the contract changes.
- Use `BASE_URL=http://localhost:5173` for Playwright when reusing the running API/web. Otherwise its configuration starts a Debug API build. The seed contains 91 species; production-scale Pokédex UI tests need a full import. Azure hosting-header checks need deployed hosting. Report unrun or skipped checks accurately.
- For legacy calculations, run `python scripts/golden.py check` from `legacy/keeper-filters/`. It checks frozen data outputs, not the HTML page assembler; validate page-generation changes separately. The standalone page is `legacy/keeper-filters/dist/pvp-keeper-filters.html`, which can be served in a normal browser.
- Keep migrations compatible with the previous application version. Do not put secrets in source, screenshots, commits or logs. Use configured credentials and preserve TLS/checksum verification.
- Run checks relevant to the changes. Do not add trivial tests for documentation edits; do test changed application or generator behavior. Check `git diff --check` and the final diff before committing.
