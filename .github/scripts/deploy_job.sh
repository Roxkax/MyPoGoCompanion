#!/usr/bin/env bash
# Creates or updates the daily import's Container Apps Job (job-import-qa or job-import-prod) with its image and
# settings (docs/DAILY-IMPORT.md). Used by the QA and prod deploys.
#
# Needs: RESOURCE_GROUP, JOB_NAME, IMAGE_TAG, DATABASE_URL (the environment's secret, Neon's postgresql:// URL).
# SCHEDULE: a cron expression in UTC ("0 6 * * *" for prod) for a scheduled job; empty for a job started by hand (QA).
# TRIGGER: what the job's own runs say started them in the run log (schedule for prod, manual for QA).
# Writes created=true|false to $GITHUB_OUTPUT when set (a new job runs its first import straight after the deploy).
#
# The database secret is the direct Neon host, not the pooler: the run holds a PostgreSQL session lock and one long
# transaction, which Neon's pooler (transaction mode) doesn't keep.
set -euo pipefail

: "${RESOURCE_GROUP:?}" "${JOB_NAME:?}" "${IMAGE_TAG:?}" "${DATABASE_URL:?}"
ENVIRONMENT_NAME=${ENVIRONMENT_NAME:-cae-mypogocompanion}
TRIGGER=${TRIGGER:-manual}
scripts="$(cd "$(dirname "$0")" && pwd)"

connection=$(python3 "$scripts/npgsql_connection.py" --direct)
echo "::add-mask::$connection"
secrets=("database-url=$connection")
settings=("ConnectionStrings__Database=secretref:database-url" "DailyImport__Trigger=$TRIGGER")

# Telemetry for this copy, as for the API (appinsights.sh).
# shellcheck source=appinsights.sh
source "$scripts/appinsights.sh"
copy="${JOB_NAME##*-}"
insights=$(appinsights_connection "$copy")
if [[ -n $insights ]]; then
  echo "::add-mask::$insights"
  secrets+=("appinsights-connection=$insights")
  settings+=("APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:appinsights-connection")
  echo "Telemetry: Application Insights appi-mypogocompanion-$copy."
else
  echo "::warning::Couldn't set up Application Insights for $copy (error above): the job runs without telemetry. See docs/OBSERVABILITY.md."
fi

# One run at a time, no automatic retry (the job retries its downloads itself; a failed run waits for the next day),
# 30 minutes at most; 0.5 vCPU and 1 GiB (the 19 MB game master is read whole).
common=(--image "$IMAGE_TAG" --cpu 0.5 --memory 1Gi --replica-timeout 1800 --replica-retry-limit 0 --only-show-errors -o none)

if az containerapp job show -g "$RESOURCE_GROUP" -n "$JOB_NAME" --only-show-errors -o none 2>/dev/null; then
  az containerapp job secret set -g "$RESOURCE_GROUP" -n "$JOB_NAME" --secrets "${secrets[@]}" --only-show-errors -o none
  schedule=()
  [[ -n ${SCHEDULE:-} ]] && schedule=(--cron-expression "$SCHEDULE")
  az containerapp job update -g "$RESOURCE_GROUP" -n "$JOB_NAME" "${common[@]}" "${schedule[@]}" --set-env-vars "${settings[@]}"
  created=false
  echo "Updated $JOB_NAME to ${IMAGE_TAG##*:}."
else
  if [[ -n ${SCHEDULE:-} ]]; then
    trigger=(--trigger-type Schedule --cron-expression "$SCHEDULE")
  else
    trigger=(--trigger-type Manual)
  fi
  az containerapp job create -g "$RESOURCE_GROUP" -n "$JOB_NAME" --environment "$ENVIRONMENT_NAME" "${trigger[@]}" \
    --parallelism 1 --replica-completion-count 1 "${common[@]}" \
    --secrets "${secrets[@]}" --env-vars "${settings[@]}" --args daily-import
  created=true
  echo "Created $JOB_NAME (${SCHEDULE:-started by hand}) with ${IMAGE_TAG##*:}."
fi

[[ -n ${GITHUB_OUTPUT:-} ]] && echo "created=$created" >> "$GITHUB_OUTPUT"
exit 0
