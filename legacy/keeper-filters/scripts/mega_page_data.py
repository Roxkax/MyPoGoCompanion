"""Megas tab -> build/data_mega.json.

`mega0-` returns every Pokémon that can Mega Evolve in GO, so it already covers every
released Mega and any future release. The lists below add the Megas from the main games
that GO has not released yet.

HAND-MAINTAINED: update NOT_YET / SOON when GO releases a Mega or the main games announce
new ones. The build warns when PvPoke marks one of these as released.
"""
import json
import re
import sys

import numpy as np

from common import BUILD, GM_FILE, MAX_LEN, VERIFY_DEX, fmt_ranges, is_mega, is_shadow, load_json, pvpoke_gamemaster, ranges, save_json
from searchsim import evaluate

ZA = "Legends: Z-A"
DLC = "Mega Dimension DLC"

# dex: source — Megas from the main games that GO has not released (Oct 2026)
NOT_YET = {
    36: ZA, 154: ZA, 160: ZA, 478: ZA, 500: ZA, 530: ZA, 545: ZA, 560: ZA, 604: ZA,
    668: ZA, 670: ZA, 689: ZA, 691: ZA, 701: ZA, 718: ZA, 780: ZA,
    358: DLC, 485: DLC, 491: DLC, 623: DLC, 678: DLC, 740: DLC, 768: DLC, 801: DLC, 807: DLC,
    952: DLC, 970: DLC, 978: DLC, 998: DLC,
}
# dex: (source, GO release date) — announced for GO, not out yet
SOON = {609: (ZA, "2026-10-31")}
# Listed by number in case `mega0-` does not return Primal Reversion
PRIMALS = [382, 383]


def gm_temp_evos():
    """Pokédex numbers whose GO game-master entry already carries Mega/Primal data."""
    out = set()
    for t in load_json(GM_FILE):
        ps = t["data"].get("pokemonSettings")
        if ps and ps.get("tempEvoOverrides") and any("stats" in o for o in ps["tempEvoOverrides"]):
            m = re.match(r"^V(\d{4})_POKEMON_", t["templateId"])
            if m:
                out.add(int(m.group(1)))
    return out


def main():
    gm = pvpoke_gamemaster()["pokemon"]
    species = {p["speciesId"]: p for p in gm}
    names = {}
    for p in sorted(gm, key=lambda p: ("_" in p["speciesId"], len(p["speciesId"]))):
        names.setdefault(p["dex"], p["speciesName"])
    released = sorted({p["dex"] for p in gm if is_mega(p) and p.get("released")})
    released_forms = sorted(p["speciesName"] for p in gm if is_mega(p) and p.get("released"))

    stale = sorted(d for d in list(NOT_YET) + list(SOON) if d in released)
    if stale:
        print(f"mega_page_data: WARNING, now released in GO, remove from NOT_YET/SOON: {stale}")
    future = sorted(set(NOT_YET) | set(SOON))
    extra = sorted(set(future) | set(PRIMALS))
    string = "mega0-," + fmt_ranges(ranges(extra))

    def pre_evos(dex):
        out = set()
        for p in gm:
            if p["dex"] != dex or is_mega(p) or is_shadow(p):
                continue
            par, seen = p.get("family", {}).get("parent"), set()
            while par in species and par not in seen:
                seen.add(par)
                out.add(species[par]["dex"])
                par = species[par].get("family", {}).get("parent")
        return out

    all_megas = sorted(set(released) | set(future))
    pre = sorted(set().union(*(pre_evos(d) for d in all_megas)) - set(all_megas))
    string_pre = string + "," + fmt_ranges(ranges(pre)) if pre else string

    dex = np.arange(1, VERIFY_DEX + 1)
    flags = {"mega0-": np.isin(dex, released)}
    for s, want in ((string, set(released) | set(extra)), (string_pre, set(released) | set(extra) | set(pre))):
        if len(s) > MAX_LEN or not np.array_equal(evaluate(s, dex, np.zeros((1, 3), int), flags=flags),
                                                   np.isin(dex, sorted(want))):
            raise SystemExit(f"Mega string verification failed: {s[:80]}…")

    in_files = gm_temp_evos()
    out = {
        "string": string, "stringWithPre": string_pre,
        "released": [{"dex": d, "name": names[d]} for d in released], "releasedForms": released_forms,
        "notYet": [{"dex": d, "name": names[d], "source": NOT_YET.get(d) or SOON[d][0],
                    "soon": SOON[d][1] if d in SOON else None, "inGoFiles": d in in_files} for d in future],
        "preEvoCount": len(pre),
    }
    save_json(out, BUILD / "data_mega.json")
    print(f"mega_page_data: {len(released)} released Megas, {len(future)} not yet in GO "
          f"({sum(1 for d in future if d in in_files)} already in GO files); string {len(string)} chars, "
          f"with pre-evolutions {len(string_pre)}; verified")
    return 0


if __name__ == "__main__":
    sys.exit(main())
