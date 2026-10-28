#!/usr/bin/env bash
# Downloads the game sprites used by the design mock-ups into ./sprites (not committed: copyrighted game art).
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p sprites
base="https://raw.githubusercontent.com/PokeMiners/pogo_assets/master/Images/Pokemon%20-%20256x256"
# file name: <dex>[_<form>] -> pokemon_icon_<dex3>_<form2>.png (00 = normal, 51/52 = Mega/Mega X, Mega Y)
for id in 059_HISUIAN 157_HISUIAN 136 146 229_51 244 003_51 007 008 009 009_51 633 246 374 704 782 885 996 607 001 002 003 004 005 006 006_51 006_52 094 094_51 382 383 361 443 444 445 445_51 609 254_51 644 807 243 466 652 893 894 796 308 464 131 359 142 645_11 323 721 485 905_THERIAN 812 623 930 034 844 450 249 537 031 770 423_11 949 344 929 536 769 843 928 948 235 330 194 195 259 260 339 340 462 389 254 809 334 038 845 778_DISGUISED 959 222_GALARIAN 823 671_RED 197 184 979 980 645_12 530 473; do
  dex=${id%%_*}; form=00; [[ $id == *_* ]] && form=${id##*_}
  out="sprites/${id}.png"
  [[ -s $out ]] && continue
  curl -sfL -o "$out" "$base/pokemon_icon_${dex}_${form}.png" && continue
  # newer assets: Addressable Assets/pm<dex>[.fMEGA].icon.png
  n=$((10#$dex)); suffix=""; [[ $form == 51 ]] && suffix=".fMEGA"; [[ $form =~ ^[A-Z_]+$ ]] && suffix=".f$form"
  curl -sfL -o "$out" "$base/Addressable%20Assets/pm${n}${suffix}.icon.png" || { rm -f "$out"; echo "missing $id"; }
done
ls sprites | wc -l

# shiny variants used on Pokémon pages
for id in 445 445_51; do
  dex=${id%%_*}; form=00; [[ $id == *_* ]] && form=${id##*_}
  out="sprites/${id}_shiny.png"
  [[ -s $out ]] || curl -sfL -o "$out" "$base/pokemon_icon_${dex}_${form}_shiny.png" || { n=$((10#$dex)); sfx=""; [[ $form == 51 ]] && sfx=".fMEGA"; curl -sfL -o "$out" "$base/Addressable%20Assets/pm${n}${sfx}.s.icon.png" || { rm -f "$out"; echo "missing ${id}_shiny"; }; }
done
python3 "$(dirname "$0")/trim_sprites.py"
