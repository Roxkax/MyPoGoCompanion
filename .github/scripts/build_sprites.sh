#!/usr/bin/env bash
# Builds the web app's Pokémon pictures (docs/SPRITES.md, decision 2026-11-07): downloads PokeMiners' picture folder
# (only the files the app uses: event costumes without their shiny pictures, no female variants) at one commit and turns it into cropped,
# square WebP files with the jobs' "sprites" command. Used by the QA and prod deploys before the web build, and by
# hand for a local copy:
#
#   .github/scripts/build_sprites.sh web/public/sprites
#
# Arguments: the output folder (made if missing), then optionally a work folder for the download (kept between
# runs, so a second run only fetches what changed).
# POKEMINERS_COMMIT: the commit to build; default: the latest. Each download is tried up to 3 times.
# Needs git and the .NET SDK. The pictures are copyrighted game art: never commit the output.
set -euo pipefail

out=${1:?"usage: build_sprites.sh <output folder> [work folder]"}
work=${2:-${RUNNER_TEMP:-${TMPDIR:-/tmp}}/pokeminers}
repo=https://github.com/PokeMiners/pogo_assets.git
folder="Images/Pokemon - 256x256/Addressable Assets"
root=$(cd "$(dirname "$0")/../.." && pwd)

# Up to 3 tries with 10 s and 20 s pauses: GitHub sometimes drops a clone.
retry() {
  local attempt
  for attempt in 1 2 3; do
    "$@" && return 0
    ((attempt < 3)) && { echo "Failed (try $attempt of 3): $*" >&2; sleep $((attempt * 10)); }
  done
  return 1
}

commit=${POKEMINERS_COMMIT:-$(retry git ls-remote "$repo" HEAD | cut -f1)}
[[ $commit =~ ^[0-9a-f]{40}$ ]] || { echo "Couldn't find PokeMiners' latest commit (got '$commit')." >&2; exit 1; }
echo "PokeMiners commit $commit"

if [[ ! -d $work/.git ]]; then
  # No file contents yet: the sparse checkout below fetches only the pictures (about 150 MB of 3,000 files).
  retry git clone --quiet --filter=blob:none --no-checkout --depth 1 "$repo" "$work"
fi
retry git -C "$work" fetch --quiet --depth 1 --filter=blob:none origin "$commit"
git -C "$work" sparse-checkout set --no-cone \
  "/$folder/pm*.icon.png" \
  "!/$folder/*.c[A-Z]*.s.icon.png" \
  "!/$folder/*.g2.*"
retry git -C "$work" -c advice.detachedHead=false checkout --quiet --detach "$commit"

mkdir -p "$out"
# A fresh folder each time: pictures PokeMiners removed don't linger.
find "$out" -maxdepth 1 \( -name '*.webp' -o -name 'set.json' \) -delete
dotnet run --project "$root/jobs/MyPoGoCompanion.Jobs" -c Release -- sprites --source "$work/$folder" --out "$out" --set "$commit"
du -sh "$out" | cut -f1 | xargs echo "Pictures folder size:"
