#!/usr/bin/env bash
# Starts one run of the daily import's Container Apps Job, waits for it and shows its run log (docs/DAILY-IMPORT.md).
# Used by the QA deploy, the prod deploy (when the import changed) and the "Run daily import" workflow (by hand and
# prod's 06:00 UTC schedule).
#
# Needs: RESOURCE_GROUP, JOB_NAME, DATABASE_URL (to read the run log), TRIGGER (schedule, deploy or manual: kept in the
# run log), and an Azure sign-in. PUBLISH_MISSING=true publishes the rows the last held run showed as missing.
# Writes result=succeeded|held|failed|skipped to $GITHUB_OUTPUT and the run log row to $IMPORT_RUN_FILE (when set), for the
# owner's alert (import_alert.py). Exits 1 when the run failed or didn't finish in time; a held run (rows missing,
# nothing published, the super admin decides) is not a failure.
set -euo pipefail

: "${RESOURCE_GROUP:?}" "${JOB_NAME:?}" "${DATABASE_URL:?}" "${TRIGGER:?}"
scripts="$(cd "$(dirname "$0")" && pwd)"
subscription=$(az account show --query id -o tsv)
job="https://management.azure.com/subscriptions/$subscription/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.App/jobs/$JOB_NAME"
api="api-version=2024-03-01"

# The job's own container, with only the arguments changed to name the trigger. Its settings come along as they are
# (secrets are references), so nothing secret passes through here.
template=$(az rest --method get --url "$job?$api" --query properties.template -o json)
args=$(jq -cn --arg trigger "$TRIGGER" --arg publish "${PUBLISH_MISSING:-false}" \
  '["daily-import", "--DailyImport:Trigger=\($trigger)"] + (if $publish == "true" then ["--DailyImport:PublishMissing=true"] else [] end)')
body=$(jq --argjson args "$args" '{containers: [.containers[0]
  | {name, image, command, env, resources} | with_entries(select(.value != null))
  | .args = $args]}' <<<"$template")
started=$(date -u +%Y-%m-%dT%H:%M:%SZ)
execution=$(az rest --method post --url "$job/start?$api" --body "$body" --query name -o tsv)
publishing=""
[[ ${PUBLISH_MISSING:-} == true ]] && publishing=", publishing what's missing"
echo "Started $JOB_NAME run $execution ($TRIGGER$publishing) at $started."

status=""
for _ in $(seq 1 150); do # up to 25 minutes, every 10 seconds (the job's own limit is 30)
  status=$(az rest --method get --url "$job/executions/$execution?$api" --query properties.status -o tsv 2>/dev/null || echo Unknown)
  case $status in
    Succeeded | Failed | Stopped | Degraded) break ;;
  esac
  sleep 10
done
echo "Run $execution: $status."

# The run log the job wrote (the newest daily import run since this one started).
row=$(psql "$DATABASE_URL" -X -At -v ON_ERROR_STOP=1 -c "
  select row_to_json(r) from (
    select status, started_at, finished_at, error, summary from job_runs
    where job = 'DailyImport' and started_at >= '$started'::timestamptz - interval '1 minute'
    order by started_at desc limit 1) r" || true)
python3 "$scripts/import_result.py" "$status" "$row" | tee -a "${GITHUB_STEP_SUMMARY:-/dev/null}"
if [[ -n ${IMPORT_RUN_FILE:-} ]]; then
  printf '%s' "$row" > "$IMPORT_RUN_FILE"
fi

logged=""
if [[ -n $row ]]; then
  logged=$(jq -r '.status // ""' <<<"$row" || true)
fi
result=failed
if [[ $status == Succeeded && $logged == Held ]]; then
  result=held
elif [[ $status == Succeeded && $logged == Succeeded ]]; then
  result=succeeded
elif [[ $status == Succeeded && -z $logged ]]; then
  result=skipped # another run held the lock: this one did nothing and left no run log
fi
if [[ -n ${GITHUB_OUTPUT:-} ]]; then
  echo "result=$result" >> "$GITHUB_OUTPUT"
fi

if [[ $result == held ]]; then
  echo "::warning::The daily import on $JOB_NAME is held: the sources no longer list some rows, so nothing was published. The list is above; to publish it, run \"Run daily import\" with \"Publish what's missing\" ticked (docs/DAILY-IMPORT.md)."
  exit 0
fi

if [[ $result == failed ]]; then
  echo "::error::The daily import on $JOB_NAME didn't succeed ($status). Its run log is above; the job's console logs are in the Azure portal (Container Apps job $JOB_NAME → Execution history → Logs)."
  exit 1
fi
