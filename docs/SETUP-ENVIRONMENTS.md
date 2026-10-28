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

- If you still plan to **rename the repository** to `MyPoGoCompanion`, do it now (GitHub → repo **Settings** → **General** → Repository name). Azure trusts the repository by its exact name, so a later rename means re-running step 2 with the new name.
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
git clone https://github.com/Roxkax/PoGoCompanion.git && cd PoGoCompanion
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

If the repository was renamed, add `githubRepository=Roxkax/MyPoGoCompanion` after `alertEmail=…` in the deployment command. Running the deployment again is safe: it only changes what differs.

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

## What lives where

- Azure template: `infra/main.bicep`
- Check workflow: `.github/workflows/environment-check.yml`
- How the environments are used: `planning/CI-CD.md`
