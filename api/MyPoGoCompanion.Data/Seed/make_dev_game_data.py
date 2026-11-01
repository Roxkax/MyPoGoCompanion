"""Writes dev-game-data.json: the dev seed's game data, cut from the golden-file inputs and outputs.

The local database needs real-looking game data before the daily import (E02-F01) exists. This takes the species
of the design's sample dataset (design/source/CONVENTIONS.md) from the frozen PvPoke and game master files in
tests/golden/keeper-filters/ and the Python build's outputs there, so the numbers are real:

    python api/MyPoGoCompanion.Data/Seed/make_dev_game_data.py

Not real (dev only): evolution candy costs follow the usual pattern, PvP scores and movesets are made up
(the frozen rankings only keep the order), Dynamax and Gigantamax flags are set by hand, and Portuguese names are
only filled in for types. The daily import replaces all of it with the real data.
"""

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GOLDEN = ROOT / "tests" / "golden" / "keeper-filters"
PVPOKE = GOLDEN / "inputs" / "pvpoke" / "src"
OUT = Path(__file__).with_name("dev-game-data.json")

# Families in the sample dataset (Today, Pokédex, raid counters, tier lists, eggs, research, collection).
SAMPLE_DEX = sorted({
    1, 2, 3, 4, 5, 6, 37, 38, 56, 57, 81, 82, 92, 93, 94, 125, 131, 133, 183, 184, 194, 195, 197, 239, 243,
    246, 247, 248, 252, 253, 254, 298, 307, 308, 333, 334, 359, 361, 362, 374, 375, 376, 382, 383, 387, 388,
    389, 443, 444, 445, 462, 466, 478, 529, 530, 607, 608, 609, 633, 634, 635, 644, 645, 650, 651, 652, 704,
    705, 706, 778, 782, 783, 784, 796, 807, 808, 809, 821, 822, 823, 845, 885, 886, 887, 893, 894, 979, 980,
    996, 997, 998,
})

TYPES = [  # id, English, Portuguese (pt-BR game text)
    ("normal", "Normal", "Normal"), ("fire", "Fire", "Fogo"), ("water", "Water", "Água"),
    ("electric", "Electric", "Elétrico"), ("grass", "Grass", "Planta"), ("ice", "Ice", "Gelo"),
    ("fighting", "Fighting", "Lutador"), ("poison", "Poison", "Venenoso"), ("ground", "Ground", "Terrestre"),
    ("flying", "Flying", "Voador"), ("psychic", "Psychic", "Psíquico"), ("bug", "Bug", "Inseto"),
    ("rock", "Rock", "Pedra"), ("ghost", "Ghost", "Fantasma"), ("dragon", "Dragon", "Dragão"),
    ("dark", "Dark", "Sombrio"), ("steel", "Steel", "Aço"), ("fairy", "Fairy", "Fada"),
]

GENERATION_STARTS = [1, 152, 252, 387, 494, 650, 722, 810, 906]
REGIONAL_TAGS = {"alolan", "galarian", "hisuian", "paldean"}
GIGANTAMAX = {"venusaur", "charizard", "lapras", "gengar"}
DYNAMAX = {"bulbasaur", "ivysaur", "venusaur", "charmander", "charmeleon", "charizard", "gastly", "haunter",
           "gengar", "lapras", "meditite", "medicham", "wooper", "quagsire", "beldum", "metang", "metagross",
           "corviknight", "rookidee", "corvisquire", "excadrill", "drilbur"}
SHINY = {"kyogre", "gengar_mega", "charmander", "charizard", "lapras", "gible", "absol", "venusaur", "litwick",
         "snorunt", "bulbasaur", "deino", "larvitar", "beldum", "goomy", "jangmoo", "dreepy"}


def generation(dex):
    return sum(1 for start in GENERATION_STARTS if dex >= start)


def form_kind(entry, default_id):
    tags = set(entry.get("tags", []))
    if "mega" in tags:
        return "Primal" if entry["speciesId"].endswith("_primal") else "Mega"
    if tags & REGIONAL_TAGS:
        return "Regional"
    return "Base" if entry["speciesId"] == default_id else "Alternate"


def cp(attack, defense, stamina, cpm, iv=15):
    return max(10, int((attack + iv) * (defense + iv) ** 0.5 * (stamina + iv) ** 0.5 * cpm * cpm / 10))


def main():
    gamemaster = json.loads((PVPOKE / "data" / "gamemaster.json").read_text(encoding="utf-8"))
    cpms_src = (PVPOKE / "js" / "pokemon" / "Pokemon.js").read_text(encoding="utf-8")
    cpms = [float(x) for x in re.search(r"var\s+cpms\s*=\s*\[([^\]]+)\]", cpms_src).group(1).split(",")]
    cpm_50 = cpms[(50 - 1) * 2]

    entries = [p for p in gamemaster["pokemon"] if p["dex"] in SAMPLE_DEX and not p["speciesId"].endswith("_shadow")
               and "duplicate" not in p.get("tags", [])]
    shadow_released = {p["speciesId"][: -len("_shadow")] for p in gamemaster["pokemon"]
                       if p["speciesId"].endswith("_shadow") and p.get("released")}

    species, forms, form_moves, families = [], [], [], {}
    defaults = {}
    for entry in entries:
        defaults.setdefault(entry["dex"], entry["speciesId"])
    for entry in entries:
        dex, form_id = entry["dex"], entry["speciesId"]
        default_id = defaults[dex]
        tags = set(entry.get("tags", []))
        family = (entry.get("family") or {}).get("id") or next(
            ((e.get("family") or {}).get("id") for e in entries if e["speciesId"] == default_id and e.get("family")),
            "FAMILY_" + default_id.upper())
        if form_id == default_id:
            families[family] = True
            species.append({
                "dex": dex,
                "nameEn": re.sub(r"\s*\(.*\)$", "", entry["speciesName"]),
                "generation": generation(dex),
                "category": "Mythical" if "mythical" in tags else "UltraBeast" if "ultrabeast" in tags
                else "Legendary" if "legendary" in tags else "None",
                "familyId": family,
                "nicknames": entry.get("nicknames", []),
            })
        kind = form_kind(entry, default_id)
        types = [t for t in entry["types"] if t != "none"]
        stats = entry["baseStats"]
        forms.append({
            "id": form_id,
            "dex": dex,
            "kind": kind,
            "isDefault": form_id == default_id,
            "nameEn": entry["speciesName"],
            "type1Id": types[0],
            "type2Id": types[1] if len(types) > 1 else None,
            "attack": stats["atk"],
            "defense": stats["def"],
            "stamina": stats["hp"],
            "maxCp50": cp(stats["atk"], stats["def"], stats["hp"], cpm_50),
            "baseFormId": default_id if kind in ("Mega", "Primal") else None,
            "released": bool(entry.get("released")),
            "shadowReleased": form_id in shadow_released,
            "shinyReleased": True if form_id in SHINY else None,
            "canDynamax": form_id in DYNAMAX or form_id in GIGANTAMAX,
            "canGigantamax": form_id in GIGANTAMAX,
            "buddyDistanceKm": entry["buddyDistance"],
            "thirdMoveStardust": entry["thirdMoveCost"],
        })
        elite = set(entry.get("eliteMoves", []))
        legacy = set(entry.get("legacyMoves", []))
        for move_id in dict.fromkeys(entry["fastMoves"] + entry["chargedMoves"] + list(elite)):
            form_moves.append({
                "formId": form_id,
                "moveId": move_id,
                "availability": "Legacy" if move_id in legacy else "EliteTm" if move_id in elite else "Normal",
            })

    form_ids = {f["id"] for f in forms}
    evolutions = []
    evolves = {e["speciesId"] for e in entries if (e.get("family") or {}).get("evolutions")}
    for entry in entries:
        for target in (entry.get("family") or {}).get("evolutions", []):
            if target in form_ids:
                # The usual costs: 25 then 100 in a three-stage family, 50 in a two-stage one.
                first_step = (entry.get("family") or {}).get("parent") is None
                candy = 100 if not first_step else 25 if target in evolves else 50
                evolutions.append({"fromFormId": entry["speciesId"], "toFormId": target, "candy": candy})

    # Moves: PvP numbers from PvPoke, raid numbers from the game master's move templates.
    used_moves = {fm["moveId"] for fm in form_moves}
    pve = {}
    for template in json.loads((GOLDEN / "inputs" / "gm" / "GAME_MASTER.json").read_text(encoding="utf-8")):
        settings = template["data"].get("moveSettings")
        if settings:
            pve[re.sub(r"_FAST$", "", str(settings["movementId"]))] = settings
    aliases = {"FUTURE_SIGHT": "FUTURESIGHT", "PYRO_BALL": "PYROBALL", "TECHNO_BLAST_DOUSE": "TECHNO_BLAST_WATER"}
    moves = []
    for move in gamemaster["moves"]:
        move_id = move["moveId"]
        settings = pve.get(aliases.get(move_id, move_id))
        if move_id not in used_moves or settings is None:
            continue
        fast = move["energyGain"] > 0 or move["energy"] == 0
        buff_self = move.get("buffsSelf") or (move.get("buffs") if move.get("buffTarget") == "self" else None) or [0, 0]
        buff_opp = move.get("buffsOpponent") or (move.get("buffs") if move.get("buffTarget") == "opponent" else None) or [0, 0]
        moves.append({
            "id": move_id,
            "kind": "Fast" if fast else "Charged",
            "typeId": move["type"],
            "nameEn": move["name"],
            "raidPower": float(settings.get("power", 0)),
            "raidDurationMs": settings.get("durationMs", 0),
            "raidDamageWindowStartMs": settings.get("damageWindowStartMs", 0),
            "raidDamageWindowEndMs": settings.get("damageWindowEndMs", 0),
            "raidEnergyDelta": settings.get("energyDelta", 0),
            "pvpPower": float(move["power"]),
            "pvpEnergyDelta": move["energyGain"] if fast else -move["energy"],
            "pvpTurns": move["turns"] if fast else None,
            "pvpBuffChance": float(move["buffApplyChance"]) if "buffApplyChance" in move else None,
            "pvpSelfAttackStages": buff_self[0], "pvpSelfDefenseStages": buff_self[1],
            "pvpOpponentAttackStages": buff_opp[0], "pvpOpponentDefenseStages": buff_opp[1],
        })
    known_moves = {m["id"] for m in moves}
    form_moves = [fm for fm in form_moves if fm["moveId"] in known_moves]

    # Rankings: real order and raid numbers from the golden files; PvP scores and movesets are made up.
    def moves_of(form_id, kind):
        return [fm["moveId"] for fm in form_moves if fm["formId"] == form_id
                and next(m for m in moves if m["id"] == fm["moveId"])["kind"] == kind]

    pvp_rankings = []
    for league, cap in (("Great", 1500), ("Ultra", 2500)):
        order = json.loads((PVPOKE / "data" / "rankings" / "all" / "overall" / f"rankings-{cap}.json").read_text())
        for rank, row in enumerate(order, start=1):
            shadow = row["speciesId"].endswith("_shadow")
            form_id = row["speciesId"][: -len("_shadow")] if shadow else row["speciesId"]
            fast, charged = moves_of(form_id, "Fast"), moves_of(form_id, "Charged")
            if form_id in form_ids and fast and charged:
                pvp_rankings.append({
                    "league": league, "formId": form_id, "isShadow": shadow, "rank": rank,
                    "score": round(max(50.0, 94.0 - rank * 0.04), 1), "fastMoveId": fast[0],
                    "chargedMove1Id": charged[0], "chargedMove2Id": charged[1] if len(charged) > 1 else None,
                })

    boxes = json.loads((GOLDEN / "outputs" / "pvp_boxes.json").read_text(encoding="utf-8"))
    keeper_boxes, best_ivs = [], []
    for code, league in (("GL", "Great"), ("UL", "Ultra")):
        for dex, box in boxes["leagues"][code]["boxes"].items():
            if int(dex) not in SAMPLE_DEX:
                continue
            (a0, a1), (d0, d1), (s0, s1) = box["box"]
            keeper_boxes.append({"league": league, "dex": int(dex), "attackBarMin": a0, "attackBarMax": a1,
                                 "defenseBarMin": d0, "defenseBarMax": d1, "staminaBarMin": s0, "staminaBarMax": s1,
                                 "judgedBy": box["sources"]})
            for form_id, rank1 in box["rank1"].items():
                if form_id in form_ids and not any(b["league"] == league and b["formId"] == form_id for b in best_ivs):
                    form = next(f for f in forms if f["id"] == form_id)
                    a, d, s = rank1["iv"]
                    cpm = cpms[int(round((rank1["level"] - 1) * 2))]
                    product = (form["attack"] + a) * cpm * (form["defense"] + d) * cpm * int((form["stamina"] + s) * cpm)
                    best_ivs.append({"league": league, "formId": form_id, "attackIv": a, "defenseIv": d,
                                     "staminaIv": s, "level": rank1["level"], "cp": rank1["cp"],
                                     "statProduct": round(product, 2)})

    raiders = json.loads((GOLDEN / "outputs" / "raiders.json").read_text(encoding="utf-8"))
    raid_rankings = []
    for type_id, ranked in raiders["types"].items():
        for rank, row in enumerate(ranked["ranked"], start=1):
            form_id = row["speciesId"][: -len("_shadow")] if row["shadow"] else row["speciesId"]
            if form_id in form_ids and row["fast"] in known_moves and row["charged"] in known_moves:
                raid_rankings.append({"attackingTypeId": type_id, "formId": form_id, "isShadow": row["shadow"],
                                      "rank": rank, "fastMoveId": row["fast"], "chargedMoveId": row["charged"],
                                      "dps": row["dps"], "tdo": row["tdo"], "score": row["er"]})

    strings = json.loads((GOLDEN / "outputs" / "pvp_strings.json").read_text(encoding="utf-8"))
    raid_page = json.loads((GOLDEN / "outputs" / "data_raid.json").read_text(encoding="utf-8"))
    mega_page = json.loads((GOLDEN / "outputs" / "data_mega.json").read_text(encoding="utf-8"))
    keeper_filters = []
    for code, kind in (("GL", "GreatLeague"), ("UL", "UltraLeague")):
        for part, row in enumerate(strings[code]["strings"], start=1):
            keeper_filters.append({"kind": kind, "variant": "", "part": part, "fromDex": row["from"],
                                   "toDex": row["to"], "searchString": row["text"]})
    for key, kind in (("raid", "Raid"), ("max", "MaxBattle")):
        keeper_filters.append({"kind": kind, "variant": "", "part": 1, "searchString": raid_page[key]["11-15"]})
        keeper_filters.append({"kind": kind, "variant": "attack-15", "part": 1, "searchString": raid_page[key]["15"]})
    keeper_filters.append({"kind": "Mega", "variant": "", "part": 1, "searchString": mega_page["string"]})
    keeper_filters.append({"kind": "Mega", "variant": "with-pre-evolutions", "part": 1,
                           "searchString": mega_page["stringWithPre"]})

    data = {
        "types": [{"id": t[0], "nameEn": t[1], "namePt": t[2], "sortOrder": i} for i, t in enumerate(TYPES, start=1)],
        "families": sorted(families),
        "species": species,
        "forms": forms,
        "moves": moves,
        "formMoves": form_moves,
        "evolutions": evolutions,
        "cpMultipliers": [{"level": 1 + i / 2, "value": v} for i, v in enumerate(cpms) if 1 + i / 2 <= 51],
        "pvpRankings": pvp_rankings,
        "pvpBestIvs": best_ivs,
        "pvpKeeperBoxes": keeper_boxes,
        "raidRankings": raid_rankings,
        "keeperFilters": keeper_filters,
    }
    # One record per line: small, and readable in a diff.
    lines = []
    for i, (key, rows) in enumerate(data.items()):
        items = ",\n".join("  " + json.dumps(row, ensure_ascii=False, separators=(",", ":")) for row in rows)
        lines.append(f' "{key}": [\n{items}\n ]' + ("," if i < len(data) - 1 else ""))
    OUT.write_text("{\n" + "\n".join(lines) + "\n}\n", encoding="utf-8")
    print(f"{OUT.name}: {len(species)} species, {len(forms)} forms, {len(moves)} moves, "
          f"{len(raid_rankings)} raid rankings, {len(pvp_rankings)} PvP rankings")


if __name__ == "__main__":
    main()
