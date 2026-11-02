#!/usr/bin/env bash
# Sourced by deploy_api.sh and deploy_job.sh: `appinsights_connection <qa|prod>` prints the connection string of that
# copy's Application Insights (appi-mypogocompanion-<copy>), creating it from infra/monitoring.bicep if it's missing,
# and prints a warning instead when that fails: telemetry isn't worth a failed deploy (the app still runs and logs to
# the console, docs/OBSERVABILITY.md). The caller masks the value. Needs: RESOURCE_GROUP, and an Azure sign-in.

appinsights_connection() {
  local copy=$1 template insights
  template="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/infra/monitoring.bicep"
  if insights=$(az deployment group create -g "$RESOURCE_GROUP" -n "monitoring-$copy" --template-file "$template" \
    --parameters "{\"environments\": {\"value\": [\"$copy\"]}}" \
    --query 'properties.outputs.connectionStrings.value[0]' -o tsv --only-show-errors) && [[ -n $insights ]]; then
    printf '%s' "$insights"
  fi
}
