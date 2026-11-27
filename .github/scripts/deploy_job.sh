#!/usr/bin/env bash
# Deploys the import or researcher Container Apps Job to QA or production. GitHub handles the schedules.
#
# Needs: RESOURCE_GROUP, JOB_NAME, IMAGE_TAG, DATABASE_URL (the environment's secret, Neon's postgresql:// URL).
# TRIGGER: what a run says started it in the run log when nothing else says (manual); every start passes its own.
# Both jobs are started by hand only: prod's 06:00 UTC schedule is the "Run daily import" workflow (decision
# 2026-11-02), so a job created earlier with an Azure schedule is switched to manual here.
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
command=daily-import
settings=("ConnectionStrings__Database=secretref:database-url")
if [[ $JOB_NAME == job-research-* ]]; then
  command=weekly-research
  settings+=("Ai__MonthlyCapUsd=${AI_MONTHLY_CAP_USD:-5}")
  if [[ -n ${OPENAI_API_KEY:-} ]]; then
    secrets+=("openai-api-key=$OPENAI_API_KEY")
    settings+=("Ai__OpenAiApiKey=secretref:openai-api-key")
  else
    settings+=("Ai__OpenAiApiKey=")
  fi
else
  settings+=("DailyImport__Trigger=$TRIGGER")
fi

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

trigger_type=$(az containerapp job show -g "$RESOURCE_GROUP" -n "$JOB_NAME" --query properties.configuration.triggerType -o tsv --only-show-errors 2>/dev/null || true)
if [[ -n $trigger_type ]]; then
  if [[ $trigger_type != Manual ]]; then
    # Created with Azure's schedule before the schedule moved to GitHub: started by hand from now on. The job stays
    # the same resource, so the API's permission to start it (infra/grant-run-now.sh) is kept.
    job_id=$(az containerapp job show -g "$RESOURCE_GROUP" -n "$JOB_NAME" --query id -o tsv)
    az rest --method patch --url "https://management.azure.com$job_id?api-version=2024-03-01" --only-show-errors -o none --body \
      '{"properties":{"configuration":{"triggerType":"Manual","manualTriggerConfig":{"parallelism":1,"replicaCompletionCount":1},"scheduleTriggerConfig":null}}}'
    echo "Switched $JOB_NAME from Azure's schedule to started by hand (the schedule is the Run daily import workflow)."
  fi
  az containerapp job secret set -g "$RESOURCE_GROUP" -n "$JOB_NAME" --secrets "${secrets[@]}" --only-show-errors -o none
  az containerapp job update -g "$RESOURCE_GROUP" -n "$JOB_NAME" "${common[@]}" --set-env-vars "${settings[@]}"
  created=false
  echo "Updated $JOB_NAME to ${IMAGE_TAG##*:}."
else
  az containerapp job create -g "$RESOURCE_GROUP" -n "$JOB_NAME" --environment "$ENVIRONMENT_NAME" --trigger-type Manual \
    --parallelism 1 --replica-completion-count 1 "${common[@]}" \
    --secrets "${secrets[@]}" --env-vars "${settings[@]}" --args "$command"
  created=true
  echo "Created $JOB_NAME (started by hand) with ${IMAGE_TAG##*:}."
fi

[[ -n ${GITHUB_OUTPUT:-} ]] && echo "created=$created" >> "$GITHUB_OUTPUT"
exit 0
