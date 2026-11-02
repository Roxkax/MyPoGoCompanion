# Observability and errors

How the API, the jobs and the web app record what happens and what goes wrong (E01-F08, decisions 2026-11-02), what
you see in the app when something breaks, and how to find an error in Azure.

## What you see in the app

| What happened | What the app shows |
|---|---|
| A screen breaks while drawing | **Something went wrong** inside the normal frame (tab bar / sidebar stay), with **Reload**, **Go to Today** and a **reference** (`Reference 7f3a2c91`). The error is reported to the API. |
| The internet works but the API doesn't answer (no answer, or Azure's gateway answers 502 / 503 / 504) | **MyPoGoCompanion isn't answering right now** in place of the screen, with **Try again**. It checks every 10 seconds and reloads by itself when the API is back. A reference shows only when the failed answer carried one (no answer at all leaves nothing to look up). |
| No internet | **You're offline** (H3, E01-F06), unchanged. |
| The API answers a request with an error (500…) | Screens that load data show **This couldn't be loaded** with **Try again** and the API's reference (`web/src/components/LoadError.tsx`; used by the data screens as they are built). |
| A script error or a failed promise nobody handled | Nothing on screen; it is reported. |

The `/design` gallery (QA and local copies) shows the "couldn't be loaded" card.

### Making an error on purpose (QA and local only)

Add `?fail=` to any address. Never works in production.

| Address | What happens |
|---|---|
| `/pokedex?fail=screen` | The screen breaks: "Something went wrong", reported as `Screen` with the reference shown. |
| `/settings?fail=script` | An error nobody catches: nothing on screen, reported as `Script`. |
| `/?fail=api` | "isn't answering", as if the API had stopped (nothing is reported). Remove `?fail=api` from the address to stop it; Try again reloads the same address. |

## What gets recorded

| Where | What |
|---|---|
| **Application Insights** `appi-mypogocompanion-prod` / `appi-mypogocompanion-qa` | API requests (address, status, duration; not `/health/live` nor CORS preflights), database calls (Npgsql), outgoing calls (Google sign-in), errors with stack traces, logs at Information and above, and the web app's error reports. Role name `api` (jobs will use their own). |
| **Container Apps console logs** (`ContainerAppConsoleLogs_CL` in the log workspace) | One JSON line per log entry. With Application Insights on, only warnings and errors (so nothing is stored twice); everything locally and in tests. |

Code: `api/MyPoGoCompanion.Observability` (shared by the API and, from E02-F01, the jobs: `builder.AddTelemetry("<role>")`),
`api/MyPoGoCompanion.Api/Diagnostics` (requests, `enduser.id`, `POST /client-errors`), `web/src/errors` (reporter,
references, `?fail=`).

Telemetry is sent only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set; the deploys set it. Locally and in tests
nothing leaves the machine.

### No personal data

Nothing that identifies a person is sent: no emails, names, Google ids, tokens, IP addresses, query strings or
collection data. Requests of a signed-in person carry only the account's **random user id** (`enduser.id`, shown as
*Authenticated user ID* in Application Insights), so one person's problems can be followed without saying who they are.

How it's enforced:

- Our own log messages never include personal data (they log ids, statuses and counts).
- Before anything is exported, a filter (`PersonalData.Scrub`) replaces email addresses with `[email]`, JWTs, bearer
  tokens and other long random strings (refresh tokens, Google's codes) with `[token]` and IPv4 addresses with `[ip]`,
  in log messages, log values, exception messages and stack traces, and in every request/dependency value.
  GUIDs (user ids), trace ids and version numbers are kept.
- Requests lose the client's address, port and query string; outgoing calls lose their query string.
- Azure blanks IP addresses on arrival anyway (`DisableIpMasking: false`, the default, kept in `infra/monitoring.bicep`).
- The web app sends only the address's **path** with a report (no query or `#`), and the API cuts it again.
- Tests: `MyPoGoCompanion.Observability.UnitTests` (the filter, the processors, the console format) and the integration
  test `ObservabilityTests` (a real sign-in, a request and a report full of personal data: nothing personal reaches the
  exporter, the user id does).

## Finding an error

1. Azure portal → resource group `rg-mypogocompanion` → **appi-mypogocompanion-prod** (or `-qa`).
2. **Failures** shows failed requests and exceptions by operation; **Transaction search** finds one by text.
3. With a reference from the app, open **Logs** and run:

```kusto
union requests, exceptions, traces
| where timestamp > ago(7d)
| where operation_Id startswith "7f3a2c91" or message has "7f3a2c91"
| order by timestamp desc
```

An API error's reference is the start of the request's operation id (the API's `traceId` in the problem details);
a screen or script error's reference is in the report's message, `Web app error 7f3a2c91 (Screen) on /pokedex, …`.

Web app reports are logged at **Error** level under the category `MyPoGoCompanion.WebApp` (event id 9001), at most
10 a minute per address and 10 per page load.

## Cost: the free 5 GB a month

Application Insights stores its data in the log workspace `log-mypogocompanion`, together with the Container Apps
console logs. Azure's free allowance is **5 GB a month** per billing account; the workspace is capped at **0.16 GB a
day** (about 5 GB a month). Real use is far below that. When the cap is reached, logging pauses until the next day
(UTC); the app keeps working. No metrics or page views are collected, and `/health/live` isn't recorded, to keep the
volume small. If traffic ever grows, sampling can be added to `TelemetrySetup` before raising the cap.

## Setup (automatic)

`infra/monitoring.bicep` holds the log workspace (30 days, 0.16 GB/day cap) and one Application Insights per copy.
The QA and prod deploys run it for their own copy (`.github/scripts/deploy_api.sh`) and pass the connection string
to the API as the Container Apps secret `appinsights-connection`, so nothing has to be set up by hand.
`infra/main.bicep` uses the same file for both copies.

If a deploy warns *"Couldn't set up Application Insights"*, the API still deploys and runs without telemetry. The
usual cause on a new subscription is `MissingSubscriptionRegistration` for `Microsoft.Insights`; register it once in
Cloud Shell, then run the deploy again:

```bash
az provider register --namespace Microsoft.Insights
```

No alerts are set up (decision 2026-11-02): errors are looked at in the portal. A health screen in the admin panel is
a later, Good to have feature.
