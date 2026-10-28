"""Raid and Max Battle strings, per-type lists and the numbers table -> build/data_raid.json."""
import sys

import numpy as np

from common import BUILD, MAX_LEN, VERIFY_DEX, fmt_ranges, load_json, pvpoke_gamemaster, ranges, save_json
from raid_rank import LEVEL, MEGA_OTHER, MEGA_OWN, SHADOW_ATK, SHADOW_DEF, STAB, TARGET_DEF, TOP_DEX, TYPES
from searchsim import evaluate

ATTACK = {"11-15": "3-4attack", "15": "4attack"}
MAX_STRINGS = {k: f"maxmove1-&{v}" for k, v in ATTACK.items()}
NEAR_MISSES = 5
EXAMPLE = ("psychic", "mewtwo_shadow")
BARS = np.array([[a, d, h] for a in range(5) for d in range(5) for h in range(5)])


def verify(text, wanted, attack_lo):
    dex = np.arange(1, VERIFY_DEX + 1)[:, None]
    got = evaluate(text, dex, BARS[None, :, :])
    exp = np.isin(dex, list(wanted)) & (BARS[None, :, 0] >= attack_lo)
    if len(text) > MAX_LEN or not np.array_equal(np.broadcast_to(got, exp.shape), exp):
        raise SystemExit(f"Raid string verification failed: {text[:80]}…")


def main():
    r = load_json(BUILD / "raiders.json")
    move_names = {m["moveId"]: m["name"] for m in pvpoke_gamemaster()["moves"]}
    dexes = sorted(set(r["attackers"]) | set(r["preEvos"]))
    dex_part = fmt_ranges(ranges(dexes))
    strings = {}
    for label, rule in ATTACK.items():
        s = f"{dex_part}&{rule}"
        verify(s, dexes, 3 if label == "11-15" else 4)
        strings[label] = s

    def row(x):
        return {
            "id": x["speciesId"], "name": x["name"], "dex": x["dex"], "mega": x["mega"], "shadow": x["shadow"],
            "fast": move_names.get(x["fast"], x["fast"]), "charged": move_names.get(x["charged"], x["charged"]),
            "eliteFast": x["eliteFast"], "eliteCharged": x["eliteCharged"],
            "dps": round(x["dps"], 2), "tdo": round(x["tdo"], 1), "er": round(x["er"], 2),
            "top": x["top"], "megaPick": x["megaPick"], "preEvos": x["preEvos"],
        }

    types = {}
    for t in TYPES:
        ranked = r["types"][t]["ranked"]
        rows, misses, seen = [], 0, set()
        top_dex = {x["dex"] for x in ranked if x["top"]}
        for x in ranked:
            if x["top"] or x["mega"]:
                rows.append(row(x))
                rows[-1]["firstOfDex"] = x["top"] and x["dex"] not in seen
                if x["top"]:
                    seen.add(x["dex"])
            elif x["dex"] not in top_dex and misses < NEAR_MISSES:
                rows.append(row(x))
                misses += 1
        picks = [x for x in ranked if x["top"] or x["megaPick"]]
        sel_dex = sorted({x["dex"] for x in picks})
        pre = sorted({d for x in picks for d in x["preEvos"]} - set(sel_dex))
        types[t] = {"tenth": round(r["types"][t]["tenth"], 2), "rows": rows,
                    "attackers": sel_dex, "preEvos": pre}

    t, sid = EXAMPLE
    ex = next(x for x in r["types"][t]["ranked"] if x["speciesId"] == sid)
    example = {"type": t, "name": ex["name"], "fast": move_names.get(ex["fast"]), "charged": move_names.get(ex["charged"]),
               "dps": ex["dps"], "tdo": ex["tdo"], "er": ex["er"], **ex["detail"]}

    out = {
        "raid": strings, "max": MAX_STRINGS, "dexCount": len(dexes),
        "attackerCount": len(r["attackers"]), "preEvoCount": len(r["preEvos"]),
        "types": types, "example": example,
        "params": {"level": LEVEL, "iv": 15, "targetDef": TARGET_DEF, "stab": STAB, "megaOwn": MEGA_OWN,
                   "megaOther": MEGA_OTHER, "shadowAtk": SHADOW_ATK, "shadowDef": SHADOW_DEF, "topDex": TOP_DEX},
    }
    save_json(out, BUILD / "data_raid.json")
    print(f"raid_page_data: raid string {len(strings['11-15'])} chars ({len(dexes)} Pokédex numbers), verified")
    return 0


if __name__ == "__main__":
    sys.exit(main())
