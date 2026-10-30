#!/usr/bin/env bash
# Quick checks that a deployed web app + API work together. Run after every deploy (QA and prod).
#
#   API_URL=https://api-qa.….azurecontainerapps.io WEB_URL=https://….azurestaticapps.net \
#   EXPECTED_VERSION=<git sha> tests/smoke/smoke.sh
#
# EXPECTED_VERSION is optional: when set, waits until the API reports that version (the new revision is live).
# Scale-to-zero means the first calls can be slow (API and Neon both wake up), so the API is retried for a while.
set -euo pipefail

API_URL="${API_URL:?Set API_URL}"
WEB_URL="${WEB_URL:?Set WEB_URL}"
EXPECTED_VERSION="${EXPECTED_VERSION:-}"
WAIT_SECONDS="${WAIT_SECONDS:-180}"
API_URL="${API_URL%/}"
WEB_URL="${WEB_URL%/}"
WEB_ORIGIN="$(sed -E 's#^(https?://[^/]+).*#\1#' <<<"$WEB_URL")"

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

failures=0
pass() { echo "✔ $1"; }
fail() { echo "::error::Smoke test: $1"; failures=$((failures + 1)); }

# 1. API health: Healthy (database reachable) and, if asked, the expected version.
deadline=$((SECONDS + WAIT_SECONDS))
body=""
while :; do
  rm -f "$tmp/health.json"
  code=$(curl -sS -o "$tmp/health.json" -w '%{http_code}' --max-time 30 "$API_URL/health" || true)
  body=$(cat "$tmp/health.json" 2>/dev/null || true)
  status=$(jq -r '.status // empty' <<<"$body" 2>/dev/null || true)
  version=$(jq -r '.version // empty' <<<"$body" 2>/dev/null || true)
  if [[ $code == 200 && $status == Healthy && ( -z $EXPECTED_VERSION || $version == "$EXPECTED_VERSION" ) ]]; then
    break
  fi
  if ((SECONDS >= deadline)); then
    break
  fi
  echo "… waiting for the API (HTTP $code, status '${status:-?}', version '${version:-?}')"
  sleep 10
done
if [[ $code == 200 && $status == Healthy ]]; then
  pass "API healthy, database reachable (version $version)"
  if [[ -n $EXPECTED_VERSION && $version != "$EXPECTED_VERSION" ]]; then
    fail "API runs version '$version', expected '$EXPECTED_VERSION'"
  fi
else
  fail "API /health answered HTTP $code: ${body:-no body}"
fi

# 2. OpenAPI description (the generated clients depend on it).
code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 30 "$API_URL/openapi/v1.json" || true)
[[ $code == 200 ]] && pass "OpenAPI description served" || fail "API /openapi/v1.json answered HTTP $code"

# 3. The web app's address may call the API (CORS).
allowed=$(curl -sS -D - -o /dev/null --max-time 30 -H "Origin: $WEB_ORIGIN" "$API_URL/health" \
  | tr -d '\r' | awk -F': ' 'tolower($1) == "access-control-allow-origin" { print $2 }' || true)
[[ $allowed == "$WEB_ORIGIN" ]] && pass "API allows calls from $WEB_ORIGIN" \
  || fail "API does not allow calls from $WEB_ORIGIN (Access-Control-Allow-Origin: '${allowed:-none}'); check Cors__AllowedOrigins"

# 4. Web app: the page and a deep link (served by the single-page fallback).
for path in / /pokedex; do
  rm -f "$tmp/web.html"
  code=$(curl -sS -o "$tmp/web.html" -w '%{http_code}' --max-time 30 "$WEB_URL$path" || true)
  if [[ $code == 200 ]] && grep -q '<div id="root">' "$tmp/web.html"; then
    pass "Web app serves $path"
  else
    fail "Web app $path answered HTTP $code or is not the app's page"
  fi
done

if ((failures > 0)); then
  echo "Smoke test failed: $failures problem(s)."
  exit 1
fi
echo "Smoke test passed."
