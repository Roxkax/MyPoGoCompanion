# Setting up Azure, Neon and GitHub

One-time setup for the prod and QA environments (Sprint 0, E00-F02). About 45 minutes, all in the browser. When you finish, run the **Environment check** workflow for `qa` and `prod`; both should go green.

What you'll create:

| Where | What | Cost |
|---|---|---|
| Neon | Project `mypogocompanion` (PostgreSQL 17) with branches `main` (prod) and `qa` | Free plan |
| Azure | Resource group `rg-mypogocompanion` with the API container apps `api-prod` and `api-qa` (scale to zero), the web app `swa-mypogocompanion` (Static Web Apps, free), a log workspace (capped at 0.5 GB/day), the GitHub sign-in identity and a €5/month budget alert | ~€0 at our usage; the budget emails you if that changes |
| GitHub | Environments `prod` and `qa` with their secrets | Free |

Container images go to **GitHub Container Registry** (free for a public repository) instead of an Azure registry, which would cost about €5/month.

## 0. Before you start

- Azure trusts the repository by the exact name GitHub puts in its sign-in token, `Roxkax@3799595/MyPoGoCompanion@1434430254` (owner and repository with their numeric IDs). If it's ever renamed, run the deployment in step 2 again with `githubRepository=Roxkax@3799595/<new name>@1434430254`. A failed **Environment check** sign-in prints the exact value GitHub sends ("assertion subject 'repo:…:environment:qa'"): the part between `repo:` and `:environment` is what goes here.
- You need an **Azure subscription**. A new free account works, but after its first 30 days Azure asks you to upgrade to pay-as-you-go to keep the free services; you're still only charged for usage above the free amounts.

## 1. Neon (database)

1. Sign in at [console.neon.tech](https://console.neon.tech) (signing in with GitHub is fine).
2. **New project**:
   - Project name: `mypogocompanion`
   - PostgreSQL version: **17**
   - Region: **AWS Europe Central 1 (Frankfurt)**, the closest to Azure West Europe
   - Database name: `mypogocompanion`
3. **Branches** (left menu):
   - The default branch must be called **`main`**. If it's called `production`, open its **⋯** menu → **Rename** → `main`.
   - If there's a `development` branch, delete it (we only use `main` and `qa`).
   - **New branch** → name `qa`, parent `main`, from the current point in time → **Create**.
4. Copy two connection strings from **Dashboard** → **Connect**:
   - Branch `main`, database `mypogocompanion`, the default role, **Connection pooling off** → copy the `postgresql://…?sslmode=require` URL. This is the **prod** `DATABASE_URL`.
   - The same with branch `qa`. This is the **QA** `DATABASE_URL`.

   Keep both somewhere private for step 3 (they contain the password). Don't paste them into chats or issues.

## 2. Azure (one command in Cloud Shell)

1. Sign in at [portal.azure.com](https://portal.azure.com) and open **Cloud Shell** (the `>_` icon in the top bar). Choose **Bash**. If asked about storage, pick **No storage account required**.
2. Paste these commands one at a time. In the last one, replace `you@example.com` with the email for budget alerts:

```bash
az provider register --namespace Microsoft.App --wait
```

```bash
az provider register --namespace Microsoft.OperationalInsights --wait
```

```bash
git clone https://github.com/Roxkax/MyPoGoCompanion.git && cd MyPoGoCompanion
```

```bash
az group create --name rg-mypogocompanion --location westeurope
```

```bash
az deployment group create --resource-group rg-mypogocompanion --template-file infra/main.bicep --parameters alertEmail=you@example.com --query properties.outputs
```

   The last command takes 3–5 minutes. When it ends it prints `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and `AZURE_SUBSCRIPTION_ID` (each with a `value`), plus the web and API addresses. Keep the three IDs for step 3. They are identifiers, not passwords.

3. Get the web app's deployment token:

```bash
az staticwebapp secrets list --name swa-mypogocompanion --query properties.apiKey -o tsv
```

   This one **is** a secret: keep it private for step 3.

Running the deployment again is safe: it only changes what differs.

## 3. GitHub environments

In the repository: **Settings** → **Environments** → **New environment**.

**`qa`**, then **Add environment secret** for each:

| Secret | Value |
|---|---|
| `AZURE_CLIENT_ID` | from step 2 |
| `AZURE_TENANT_ID` | from step 2 |
| `AZURE_SUBSCRIPTION_ID` | from step 2 |
| `DATABASE_URL` | the **qa** branch URL from step 1 |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | the token from step 2 |

**`prod`**: the same five secrets, but `DATABASE_URL` is the **main** branch URL. Then, under **Deployment branches and tags**, choose **Selected branches and tags** → **Add deployment branch or tag rule** → `main`. Only `main` can deploy to prod.

Later features add more secrets here (Google sign-in, Anthropic API key, Neon API key for the QA reset); each comes with its own short instructions.

## 4. Check

**Actions** → **Environment check** → **Run workflow**: run it once with `qa` and once with `prod`. Each run signs in to Azure without a password, finds the API and web app, connects to that environment's database and confirms the token is set. Both green means E00-F02 is done; tell Claude.

If a run fails, the step's error says which part: sign-in (IDs or the repository name in step 2), resources (step 2), database (`DATABASE_URL`) or token.

## 5. Make the API image public (once, after the first QA deploy)

The deploy pipelines push the API image to GitHub Container Registry as the package `mypogocompanion-api`. Azure pulls it without a password, so the package must be **public** (free for a public repository; the image holds no secrets: they are set on the container app). GitHub creates new packages as private, so the first **QA deploy** run stops at "Check the image is public" with an error pointing here.

1. On GitHub: your profile → **Packages** → **mypogocompanion-api** → **Package settings** (bottom right).
2. **Danger Zone** → **Change visibility** → **Public**, type the name to confirm.
3. Still in Package settings, under **Manage Actions access**, check that `MyPoGoCompanion` is listed with the **Write** role (it's added automatically by the first push).
4. Re-run the failed **QA deploy** check on the pull request.

## When a database password changes

Each Neon branch keeps its own copy of the database user, so resetting a password, or deleting and re-creating the `qa` branch, makes that environment's `DATABASE_URL` stop working ("password authentication failed" in the QA deploy or the Environment check). To fix it:

1. Neon → **Dashboard** → **Connect**, pick the branch (`qa` or `main`), database `mypogocompanion`, and copy the `postgresql://…` URL (step 1). The pooled address is fine; the pipelines use the direct one for migrations themselves.
2. GitHub → **Settings** → **Environments** → `qa` (or `prod`) → `DATABASE_URL` → **Update secret**, paste it.
3. Run the **Environment check** for that environment, then re-run the failed deploy.

## Re-running the Azure template

Running `infra/main.bicep` again (step 2) resets `api-prod` and `api-qa` to the placeholder image, port 80 and no settings. The next QA deploy (or prod deploy) puts everything back; until then that API shows the placeholder page. Only re-run it when Claude asks.

## What lives where

- Azure template: `infra/main.bicep`
- Check workflow: `.github/workflows/environment-check.yml`
- QA deploy: `.github/workflows/deploy-qa.yml`
- How the environments are used: `planning/CI-CD.md`
