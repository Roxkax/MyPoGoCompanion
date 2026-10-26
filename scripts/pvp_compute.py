"""Best appraisal box per Pokédex number per league -> build/pvp_boxes.json.

For each species and league, every one of the 4,096 IV spreads is levelled to the
highest level <= 50 that stays under the CP cap and scored by stat product
(Atk x Def x floor(HP)). Spreads scoring >= KEEP of the rank-1 spread are "keepers".
Each Pokédex number then gets the one box of appraisal bars (a contiguous bar range
per stat) with the best F1 against its keeper set, always containing the rank-1 spread.
"""
import argparse
import itertools
import sys

import numpy as np

from common import (BATTLE_ONLY, BAR_OF_IV, BUILD, LEAGUES, MAX_DEX, cpm_table, is_mega,
                    is_shadow, pvpoke_gamemaster, pvpoke_rankings, save_json)

KEEP = 0.98
MAX_LEVEL = 50
TOP_N_MIDDLE = 150
POLICIES = ("meta", "final", "all", "cap")

IVS = np.array(list(itertools.product(range(16), repeat=3)))          # (4096, 3) atk, def, hp
CELL = np.array([BAR_OF_IV[a] * 25 + BAR_OF_IV[d] * 5 + BAR_OF_IV[h] for a, d, h in IVS])
CELL_SIZE = np.bincount(CELL, minlength=125)

INTERVALS = [(lo, hi) for lo in range(5) for hi in range(lo, 5)]       # 15 per stat
BOXES = list(itertools.product(INTERVALS, repeat=3))                   # 3,375
_bar = np.arange(5)
_imask = np.array([(lo <= _bar) & (_bar <= hi) for lo, hi in INTERVALS])  # (15, 5)
BOX_MASK = np.array([(_imask[a][:, None, None] & _imask[d][None, :, None] & _imask[h][None, None, :]).ravel()
                     for a, d, h in itertools.product(range(15), repeat=3)])  # (3375, 125)
BOX_IN = BOX_MASK.astype(np.int64) @ CELL_SIZE


def species_list():
    """Ownable, non-Mega, non-Shadow PvPoke entries keyed by speciesId."""
    out = {}
    for p in pvpoke_gamemaster()["pokemon"]:
        if is_mega(p) or is_shadow(p) or p["speciesId"] in BATTLE_ONLY:
            continue
        if not 1 <= p["dex"] <= MAX_DEX:
            continue
        out[p["speciesId"]] = p
    return out


def league_stats(p, cap):
    """Stat product, level and CP of every IV spread at its best level under the cap."""
    cpms = np.array(cpm_table()[: (MAX_LEVEL - 1) * 2 + 1])
    b = p["baseStats"]
    a = b["atk"] + IVS[:, 0]
    d = b["def"] + IVS[:, 1]
    h = b["hp"] + IVS[:, 2]
    cp = np.floor(a[:, None] * np.sqrt(d * h)[:, None] * cpms[None, :] ** 2 / 10)
    cp = np.maximum(cp, 10)
    ok = cp <= cap
    idx = np.where(ok.any(axis=1), ok.shape[1] - 1 - np.argmax(ok[:, ::-1], axis=1), 0)
    m = cpms[idx]
    sp = (a * m) * (d * m) * np.maximum(np.floor(h * m), 10)
    return sp, 1 + idx / 2, cp[np.arange(len(idx)), idx]


def keepers_of(p, cap):
    sp, lvl, cp = league_stats(p, cap)
    best = int(np.argmax(sp))
    keep = sp >= KEEP * sp[best]
    return keep, best, sp, lvl, cp


def best_box(keep_mask, required_cells):
    k = np.bincount(CELL[keep_mask], minlength=125)
    tp = BOX_MASK.astype(np.int64) @ k
    fp = BOX_IN - tp
    fn = k.sum() - tp
    f1 = 2 * tp / np.maximum(2 * tp + fp + fn, 1)
    allowed = BOX_MASK[:, sorted(required_cells)].all(axis=1)
    f1 = np.where(allowed, f1, -1.0)
    # best F1, then the tightest box
    order = np.lexsort((BOX_IN, -f1))
    i = int(order[0])
    return BOXES[i], float(f1[i]), int(tp[i]), int(BOX_IN[i]), int(k.sum())


def family_tree(species):
    finals_cache = {}

    def evolutions(sid):
        return [e for e in species[sid].get("family", {}).get("evolutions", []) if e in species]

    def finals(sid, seen=()):
        if sid in finals_cache:
            return finals_cache[sid]
        ev = [e for e in evolutions(sid) if e not in seen]
        res = {sid} if not ev else set().union(*(finals(e, seen + (sid,)) for e in ev))
        finals_cache[sid] = res
        return res

    def parent(sid):
        par = species[sid].get("family", {}).get("parent")
        return par if par in species else None

    return evolutions, finals, parent


def compute(policy="meta"):
    species = species_list()
    evolutions, finals, parent = family_tree(species)
    out = {"policy": policy, "keep": KEEP, "maxLevel": MAX_LEVEL, "leagues": {}}
    for league, cap in LEAGUES.items():
        top = [r["speciesId"] for r in pvpoke_rankings(cap)][:TOP_N_MIDDLE]
        cache = {}

        def info(sid):
            if sid not in cache:
                cache[sid] = keepers_of(species[sid], cap)
            return cache[sid]

        by_dex = {}
        for sid, p in species.items():
            by_dex.setdefault(p["dex"], []).append(sid)

        boxes = {}
        for dex in range(1, MAX_DEX + 1):
            sources = set()
            for sid in by_dex.get(dex, []):
                is_final = not evolutions(sid)
                is_middle = not is_final and parent(sid) is not None
                if is_final or policy in ("all", "cap"):
                    sources.add(sid)
                if not is_final and policy != "cap":
                    sources |= finals(sid)
                if is_middle and policy == "meta" and sid in top:
                    sources.add(sid)
            if not sources:
                raise SystemExit(f"No species found for Pokédex #{dex}")
            keep = np.zeros(len(IVS), bool)
            required = set()
            rank1 = {}
            for sid in sorted(sources):
                k, best, sp, lvl, cp = info(sid)
                keep |= k
                required.add(int(CELL[best]))
                rank1[sid] = {"iv": IVS[best].tolist(), "level": float(lvl[best]), "cp": int(cp[best])}
            box, f1, tp, inbox, nkeep = best_box(keep, required)
            boxes[dex] = {
                "box": [list(b) for b in box],
                "f1": round(f1, 4), "tp": tp, "inBox": inbox, "keepers": nkeep,
                "sources": sorted(sources), "rank1": rank1,
                "forms": sorted(by_dex.get(dex, [])),
            }
        out["leagues"][league] = {"cap": cap, "boxes": boxes}
    return out


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--policy", choices=POLICIES, default="meta")
    args = ap.parse_args(argv)
    res = compute(args.policy)
    save_json(res, BUILD / "pvp_boxes.json")
    for lg, d in res["leagues"].items():
        bx = d["boxes"].values()
        anyatk = sum(1 for b in bx if b["box"][0] == [0, 4])
        mean_f1 = sum(b["f1"] for b in bx) / len(bx)
        print(f"pvp_compute [{args.policy}] {lg}: {len(bx)} Pokédex numbers, mean F1 {mean_f1:.3f}, "
              f"{anyatk} accept any Attack")
    return 0


if __name__ == "__main__":
    sys.exit(main())
