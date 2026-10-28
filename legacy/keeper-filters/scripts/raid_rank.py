"""Raid (PvE) ranking per attacking type -> build/raiders.json.

GamePress-style comprehensive DPS at level 40, 15/15/15, against a neutral target with
Defense 180. Each entry is scored with its best moveset whose fast AND charged move are
both of the type. Score (ER) = (DPS^3 * TDO)^(1/4).
Selection per type: top 10 distinct Pokédex numbers among regular and shadow forms, every
Mega/Primal scoring above the 10th, and all their pre-evolutions.
"""
import math
import re
import sys

from common import BATTLE_ONLY, BUILD, GM_FILE, cpm_at, is_mega, is_shadow, load_json, pvpoke_gamemaster, save_json

TYPES = ["normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel",
         "fire", "water", "grass", "electric", "psychic", "ice", "dragon", "dark", "fairy"]
LEVEL = 40
IV = 15
TARGET_DEF = 180
STAB = 1.2
MEGA_OWN, MEGA_OTHER = 1.3, 1.1
SHADOW_ATK, SHADOW_DEF = 1.2, 5 / 6
TOP_DEX = 10
EXCLUDED_MOVES = {"RETURN", "FRUSTRATION"}
ALIASES = {"FUTURE_SIGHT": "FUTURESIGHT", "PYRO_BALL": "PYROBALL", "TECHNO_BLAST_DOUSE": "TECHNO_BLAST_WATER"}
NO_PVE = re.compile(r"^(HIDDEN_POWER|AEGISLASH_CHARGE)")


def pve_moves():
    out = {}
    for t in load_json(GM_FILE):
        m = re.match(r"^V\d{4}_MOVE_(.+)$", t["templateId"])
        if m and "moveSettings" in t["data"]:
            s = t["data"]["moveSettings"]
            out[m.group(1)] = {
                "type": s.get("pokemonType", "").replace("POKEMON_TYPE_", "").lower(),
                "power": float(s.get("power", 0)),
                "duration": s["durationMs"] / 1000,
                "dws": s.get("damageWindowStartMs", 0) / 1000,
                "energy": int(s.get("energyDelta", 0)),
            }
    return out


def lookup(moves, move_id, fast, missing):
    if NO_PVE.match(move_id):
        return None
    key = ALIASES.get(move_id, move_id)
    key = f"{key}_FAST" if fast else key
    if key not in moves:
        missing.add(key)
        return None
    return moves[key]


def stats(p):
    cpm = cpm_at(LEVEL)
    b = p["baseStats"]
    sh = is_shadow(p)
    return ((b["atk"] + IV) * cpm * (SHADOW_ATK if sh else 1),
            (b["def"] + IV) * cpm * (SHADOW_DEF if sh else 1),
            math.floor((b["hp"] + IV) * cpm))


def multiplier(p, mtype):
    m = STAB if mtype in p["types"] else 1.0
    if is_mega(p):
        m *= MEGA_OWN if mtype in p["types"] else MEGA_OTHER
    return m


def score(p, fm, cm, fast_name=None, charged_name=None):
    """Comprehensive DPS / TDO / ER for one moveset, with every intermediate value."""
    atk, df, hp = stats(p)
    y = 900 / df
    fmult, cmult = multiplier(p, fm["type"]), multiplier(p, cm["type"])
    fdmg = math.floor(0.5 * fm["power"] * atk / TARGET_DEF * fmult) + 1
    cdmg = math.floor(0.5 * cm["power"] * atk / TARGET_DEF * cmult) + 1
    fe, ce = fm["energy"], -cm["energy"]
    if ce >= 100:
        ce = ce + 0.5 * fe + 0.5 * y * cm["dws"]
    fdps, feps = fdmg / fm["duration"], fe / fm["duration"]
    cdps, ceps = cdmg / cm["duration"], ce / cm["duration"]
    x = 0.5 * ce + 0.5 * fe
    dps0 = (fdps * ceps + cdps * feps) / (ceps + feps)
    dps = dps0 + (cdps - fdps) / (ceps + feps) * (0.5 - x / hp) * y
    if cdps < fdps:
        dps = fdps
    tdo = dps * hp / y
    er = (dps ** 3 * tdo) ** 0.25
    return {
        "fast": fast_name, "charged": charged_name, "dps": dps, "tdo": tdo, "er": er,
        "detail": {"atk": atk, "def": df, "hp": hp, "y": y, "fastMult": fmult, "chargedMult": cmult,
                   "fastPower": fm["power"], "fastDur": fm["duration"], "fastEnergy": fe, "fastDmg": fdmg,
                   "chargedPower": cm["power"], "chargedDur": cm["duration"], "chargedCost": -cm["energy"],
                   "chargedDws": cm["dws"], "ce": ce, "fdps": fdps, "feps": feps, "cdps": cdps, "ceps": ceps,
                   "x": x, "dps0": dps0},
    }


def rank():
    moves = pve_moves()
    missing = set()
    gm = pvpoke_gamemaster()["pokemon"]
    species = {p["speciesId"]: p for p in gm}
    entries = [p for p in gm if p.get("released") and p["speciesId"] not in BATTLE_ONLY]

    def pre_evos(p):
        if is_mega(p):  # PvPoke Mega entries often lack family data: use the base form
            base = re.sub(r"_(mega|primal)(_[xyz])?$", "", p["speciesId"])
            p = species.get(base) or next((q for q in gm if q["dex"] == p["dex"] and not is_mega(q)
                                           and not is_shadow(q)), p)
        out, seen = [], set()
        par = p.get("family", {}).get("parent")
        while par and par in species and par not in seen:
            seen.add(par)
            q = species[par]
            out.append(q["dex"])
            par = q.get("family", {}).get("parent")
        return out

    per_type = {}
    for t in TYPES:
        scored = []
        for p in entries:
            fast = [(m, lookup(moves, m, True, missing)) for m in p.get("fastMoves", []) if m not in EXCLUDED_MOVES]
            charged = [(m, lookup(moves, m, False, missing)) for m in p.get("chargedMoves", []) if m not in EXCLUDED_MOVES]
            fast = [(n, m) for n, m in fast if m and m["type"] == t]
            charged = [(n, m) for n, m in charged if m and m["type"] == t]
            if not fast or not charged:
                continue
            best = max((score(p, fm, cm, fn, cn) for fn, fm in fast for cn, cm in charged), key=lambda r: r["er"])
            elite = set(p.get("eliteMoves", []))
            scored.append({
                "speciesId": p["speciesId"], "name": p["speciesName"], "dex": p["dex"],
                "mega": is_mega(p), "shadow": is_shadow(p), "types": [x for x in p["types"] if x != "none"],
                "eliteFast": best["fast"] in elite, "eliteCharged": best["charged"] in elite,
                "preEvos": pre_evos(p), **best,
            })
        scored.sort(key=lambda r: -r["er"])
        dexes, tenth = [], None
        for r in scored:
            if r["mega"]:
                continue
            if r["dex"] not in dexes:
                dexes.append(r["dex"])
                if len(dexes) == TOP_DEX:
                    tenth = r["er"]
                    break
        tenth = tenth if tenth is not None else 0
        for r in scored:
            r["top"] = (not r["mega"] and r["dex"] in dexes)
            r["megaPick"] = r["mega"] and r["er"] > tenth
        per_type[t] = {"tenth": tenth, "ranked": scored}
    if missing:
        print(f"raid_rank: no PvE stats for {len(missing)} moves (skipped): {', '.join(sorted(missing))}")
    return per_type


def selection(per_type):
    attackers, pre = set(), set()
    for t, d in per_type.items():
        for r in d["ranked"]:
            if r["top"] or r["megaPick"]:
                attackers.add(r["dex"])
                pre.update(r["preEvos"])
    pre -= attackers
    return sorted(attackers), sorted(pre)


def main():
    per_type = rank()
    attackers, pre = selection(per_type)
    save_json({"level": LEVEL, "targetDef": TARGET_DEF, "types": per_type,
               "attackers": attackers, "preEvos": pre}, BUILD / "raiders.json")
    print(f"raid_rank: {len(attackers) + len(pre)} Pokédex numbers ({len(attackers)} attackers, {len(pre)} pre-evolutions)")
    for t in TYPES:
        top = [r for r in per_type[t]["ranked"] if r["top"] or r["megaPick"]][:4]
        print(f"  {t:9s} " + " | ".join(f"{r['name']} {r['er']:.1f}" for r in top))
    return 0


if __name__ == "__main__":
    sys.exit(main())
