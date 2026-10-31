# Prod deploy and rollback

How code reaches prod, what happens when something fails, and how to put an earlier version back. The workflow is `.github/workflows/deploy-prod.yml` (**Actions → Prod deploy**); the decisions behind it are in `planning/CI-CD.md`.

## What happens on every merge to `main`

| Step | What it does |
|---|---|
| Anything to deploy? | Compares `main` with the commit prod runs. If only notes changed (Markdown, `docs/`, `planning/`, `design/`), prod is left as it is. |
| deploy-prod | Builds the API image `ghcr.io/roxkax/mypogocompanion-api:<commit>` → applies the database migrations to Neon `main` → deploys the image to `api-prod` → builds the web app for the prod API and deploys it to Static Web Apps production → runs the **smoke test** against prod (API healthy with the new version, database reachable, OpenAPI, CORS, web pages). |
| Reset the QA database | Resets the Neon `qa` branch to a fresh copy of prod (planning/CI-CD.md), after waiting for any QA deploy or UI tests in progress. If QA was showing an open PR, that PR is deployed to QA again so its own migrations are back. |
| Alert the owner | If anything failed, opens an issue **"Prod deploy failed"** (label `prod-alert`) assigned to you, so GitHub notifies you by email, on the web and in the mobile app. Later failures add a comment to the same issue. The next good deploy closes it. |

Only one prod deploy runs at a time. If you merge twice in a row, the second waits; a third replaces the waiting one (it includes everything).

The run's **Summary** shows what prod ran before (`Prod ran …:<commit>`) and what it runs now. The prod web address is on the run page (the `prod` environment link) and under **Code → Deployments**.

## When it fails

Open the issue (or the run), find the red step and read its error.

| Failed step | What it means | Prod |
|---|---|---|
| Build / migration bundle | The code doesn't build or a migration is broken | Unchanged |
| Apply migrations | The migration failed on prod data (`password authentication failed` = the prod `DATABASE_URL` secret is out of date, see `docs/SETUP-ENVIRONMENTS.md`) | The previous version keeps serving. Each migration runs in its own transaction, so the failed one leaves nothing half-done (earlier migrations of the same deploy stay applied) |
| Deploy the API | Azure refused the update | The previous API keeps serving |
| Deploy the web app | Static Web Apps refused the upload | New API, previous web app |
| Smoke test | Something in the new version doesn't work | New version live: **roll back** if users are affected |
| Reset the QA database | Neon key, project ID or branch problem (the error says which) | Fine; only QA keeps its old data |

A temporary error (Azure or Neon busy): **Re-run failed jobs** on the run page. A real bug: fix it in a new pull request; merging it deploys again. If prod is broken in the meantime, roll back.

## Roll back

Puts an earlier commit of `main` back on prod: its API image (already in the registry, no rebuild) and its web app (rebuilt from that commit). About 5 minutes.

1. Find the commit to go back to: the last run of **Actions → Prod deploy** that was green before the problem, or the line `Prod ran …:<commit>` in the failed run's Summary. The first 7 characters are enough.
2. **Actions → Prod deploy → Run workflow**. Branch: `main`. Commit: paste it. **Run workflow**.
3. Wait for the green run (it ends with the smoke test against prod), then open the prod web app.

What a rollback does **not** do:

- **Undo database migrations.** The older API runs on the newer tables. That's why every migration must keep the previous version working: add new columns or tables first, stop using old ones in one PR, remove them in a later PR. If a migration itself damaged data, restore the database from Neon instead: Neon → branch `main` → **Restore** to a time before the deploy. Neon only keeps a short restore window on the free plan, so do this soon. Ask Claude before you restore.
- **Reset the QA database.** QA keeps what it has.

The rollback stays until the next merge that changes code, which deploys the latest `main` again. To undo the rollback sooner, run the workflow with the Commit field empty.

### If GitHub Actions is down

Only the API can be put back by hand. In the Azure portal open **Cloud Shell** (Bash) and run, with the full 40-character commit:

```bash
az containerapp update -g rg-mypogocompanion -n api-prod --image ghcr.io/roxkax/mypogocompanion-api:<commit>
```

The web app needs a build, so it waits until Actions works again (then use the steps above).

## Running a deploy by hand

**Actions → Prod deploy → Run workflow** on `main`, Commit empty: deploys the latest `main` again even if only notes changed (for example after fixing a secret).
