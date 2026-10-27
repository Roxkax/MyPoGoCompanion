#!/usr/bin/env bash
# Downloads the game sprites used by the design mock-ups into ./sprites (not committed: copyrighted game art).
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p sprites
base="https://raw.githubusercontent.com/PokeMiners/pogo_assets/master/Images/Pokemon%20-%20256x256"
# file name: <dex>[_<form>] -> pokemon_icon_<dex3>_<form2>.png (00 = normal, 51/52 = Mega/Mega X, Mega Y)
for id in 001 002 003 004 005 006 006_51 006_52 094 094_51 382 443 444 445 445_51 609 254_51 644 807 243 466 652 893 894 796 308 464 131 359; do
  dex=${id%%_*}; form=00; [[ $id == *_* ]] && form=${id##*_}
  out="sprites/${id}.png"
  [[ -s $out ]] && continue
  curl -sfL -o "$out" "$base/pokemon_icon_${dex}_${form}.png" && continue
  # newer assets: Addressable Assets/pm<dex>[.fMEGA].icon.png
  n=$((10#$dex)); suffix=""; [[ $form == 51 ]] && suffix=".fMEGA"
  curl -sfL -o "$out" "$base/Addressable%20Assets/pm${n}${suffix}.icon.png" || { rm -f "$out"; echo "missing $id"; }
done
ls sprites | wc -l
