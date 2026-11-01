#!/usr/bin/env bash
# Deploys the API image to a Container App (api-qa or api-prod) with its settings. Used by the QA and prod deploys.
#
# Needs: RESOURCE_GROUP, API_APP, IMAGE_TAG, DATABASE_URL, WEB_ORIGIN (the web app's address, for CORS and sign-in).
# Sign-in (E01-F02), from the GitHub environment; until they are all set the API answers "sign-in isn't set up" (503)
# and everything else works: GOOGLE_CLIENT_ID (variable), GOOGLE_CLIENT_SECRET and AUTH_SIGNING_KEY (secrets),
# SUPER_ADMIN_EMAIL (variable). Setup: docs/SIGN-IN.md.
set -euo pipefail

: "${RESOURCE_GROUP:?}" "${API_APP:?}" "${IMAGE_TAG:?}" "${DATABASE_URL:?}" "${WEB_ORIGIN:?}"

# Secrets are Container Apps secrets, referenced by the settings; plain settings are set directly.
secrets=("database-url=$DATABASE_URL")
settings=("ConnectionStrings__Database=secretref:database-url" "Cors__AllowedOrigins=$WEB_ORIGIN")

if [[ -n "${GOOGLE_CLIENT_ID:-}" && -n "${GOOGLE_CLIENT_SECRET:-}" && -n "${AUTH_SIGNING_KEY:-}" ]]; then
  secrets+=("google-client-secret=$GOOGLE_CLIENT_SECRET" "auth-signing-key=$AUTH_SIGNING_KEY")
  settings+=(
    "Auth__Google__ClientId=$GOOGLE_CLIENT_ID"
    "Auth__Google__ClientSecret=secretref:google-client-secret"
    "Auth__SigningKey=secretref:auth-signing-key"
  )
  echo "Sign-in: configured (Google client ${GOOGLE_CLIENT_ID%%.*}…)."
else
  echo "::warning::Sign-in isn't set up for this environment yet (GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, AUTH_SIGNING_KEY): the API answers 503 to sign-in. See docs/SIGN-IN.md."
fi

if [[ -n "${SUPER_ADMIN_EMAIL:-}" ]]; then
  settings+=("Auth__SuperAdminEmail=$SUPER_ADMIN_EMAIL")
else
  echo "::warning::No SUPER_ADMIN_EMAIL variable: nobody is super admin in this environment. See docs/SIGN-IN.md."
fi

az containerapp secret set -g "$RESOURCE_GROUP" -n "$API_APP" --secrets "${secrets[@]}" --only-show-errors -o none
# The API listens on 8080 (the bicep placeholder used 80).
az containerapp ingress update -g "$RESOURCE_GROUP" -n "$API_APP" --target-port 8080 --only-show-errors -o none
az containerapp update -g "$RESOURCE_GROUP" -n "$API_APP" --image "$IMAGE_TAG" --only-show-errors -o none \
  --set-env-vars "${settings[@]}"
