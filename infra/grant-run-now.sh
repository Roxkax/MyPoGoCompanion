#!/usr/bin/env bash
# Allows a copy's API to start its own daily import job, for the admin's "Run now" (E02-F04, docs/DAILY-IMPORT.md).
# Run it once per copy, as the owner, in Azure Cloud Shell (Bash): the pipeline's identity is only Contributor on the
# resource group and can't hand out permissions.
#
#   bash grant-run-now.sh qa
#   bash grant-run-now.sh prod
#
# What it does: gives api-<copy>'s own Azure identity (switched on by the deploy) the built-in role
# "Container Apps Jobs Operator" on job-import-<copy> only: it can read that job and start or stop its runs, nothing
# else. Running it again changes nothing.
set -euo pipefail

copy=${1:?Which copy: qa or prod}
[[ $copy == qa || $copy == prod ]] || { echo "Which copy: qa or prod (got \"$copy\")." >&2; exit 64; }
group=${RESOURCE_GROUP:-rg-mypogocompanion}
role="Container Apps Jobs Operator"

principal=$(az containerapp show -g "$group" -n "api-$copy" --query identity.principalId -o tsv)
if [[ -z $principal ]]; then
  echo "api-$copy has no Azure identity yet: run a $copy deploy first (it switches the identity on), then run this again." >&2
  exit 1
fi

job=$(az containerapp job show -g "$group" -n "job-import-$copy" --query id -o tsv 2>/dev/null || true)
if [[ -z $job ]]; then
  echo "There's no job-import-$copy yet: run a $copy deploy first (it creates the job), then run this again." >&2
  exit 1
fi

if [[ -n $(az role assignment list --assignee "$principal" --scope "$job" --role "$role" --query "[0].id" -o tsv) ]]; then
  echo "api-$copy may already start job-import-$copy: nothing to do."
  exit 0
fi

az role assignment create --assignee-object-id "$principal" --assignee-principal-type ServicePrincipal \
  --role "$role" --scope "$job" -o none
echo "Done: api-$copy may now start job-import-$copy (\"$role\" on that job only)."
echo "It can take a few minutes before Azure applies it; until then Run now answers that the API isn't allowed to."
