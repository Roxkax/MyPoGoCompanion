"""Writes the daily import's test fixtures: small cuts of the real source files for a handful of species.

Each fixture has the real files' shape (PvPoke's gamemaster.json, rankings and Pokemon.js, the datamined
GAME_MASTER.json and the game's Portuguese text), trimmed to the Pokémon below, their moves, Max Battle looks,
evolution tasks and names, so the tests read what the job reads without 30 MB of data. Run it with the folder of
downloaded files (docs/DAILY-IMPORT.md):

    python jobs/MyPoGoCompanion.Jobs.UnitTests/Fixtures/make_fixtures.py <folder with gamemaster.json, rankings-1500.json,
        rankings-2500.json, rankings-10000.json, Pokemon.js, GAME_MASTER.json, and the game text's release and server
        files saved as pt-br_raw.json and pt-br_remote_raw.json>

The chosen species cover: a three-stage family with a Mega and Shadows (Bulbasaur, Charmander, with Gigantamax),
a regional form (Vulpix), branching evolutions with lures, day/night and tasks (Eevee), a Mega with a signature move
(Beedrill), a species PvPoke lists in another order than the game (Zacian, Darmanitan), an item count (Gimmighoul),
a ranking padded with "none" (Unown), weather forms (Castform) and a form the game master doesn't have (Basculegion).
"""

import json
import re
import sys
from pathlib import Path

DEX = {1, 2, 3, 4, 5, 6, 13, 14, 15, 37, 38, 133, 134, 135, 136, 196, 197, 470, 471, 700, 201, 351, 554, 555, 888,
       902, 999, 1000}
OUT = Path(__file__).resolve().parent
POKEMON_FIELDS = {"pokemonId", "form", "type", "type2", "stats", "familyId", "evolutionBranch"}


def main(source: Path):
    pvpoke = json.loads((source / "gamemaster.json").read_text(encoding="utf-8"))
    pokemon = [p for p in pvpoke["pokemon"] if p["dex"] in DEX]
    used = {m for p in pokemon for key in ("fastMoves", "chargedMoves", "eliteMoves", "legacyMoves", "extraChargedMoves")
            for m in p.get(key, [])}
    ids = {p["speciesId"] for p in pokemon}
    rankings = {}
    for cap in (1500, 2500, 10000):
        rows = json.loads((source / f"rankings-{cap}.json").read_text(encoding="utf-8"))
        rankings[cap] = [{"speciesId": r["speciesId"], "speciesName": r["speciesName"], "score": r["score"],
                          "moveset": r["moveset"],
                          "moves": {kind: [u for u in r.get("moves", {}).get(kind, []) if u["moveId"] in used]
                                    for kind in ("fastMoves", "chargedMoves")}}
                         for r in rows if r["speciesId"] in ids]
        used |= {m for r in rankings[cap] for m in r["moveset"]}
    moves = [m for m in pvpoke["moves"] if m["moveId"] in used]
    write("pvpoke-gamemaster.json", {"pokemon": pokemon, "moves": moves})
    for cap, rows in rankings.items():
        write(f"pvpoke-rankings-{cap}.json", rows)
    cpms = re.search(r"var\s+cpms\s*=\s*\[[^\]]+\];?", (source / "Pokemon.js").read_text(encoding="utf-8")).group(0)
    (OUT / "pvpoke-Pokemon.js").write_text(f"// Cut of PvPoke's src/js/pokemon/Pokemon.js\n{cpms}\n", encoding="utf-8")

    game_master = json.loads((source / "GAME_MASTER.json").read_text(encoding="utf-8"))
    move_keys = set()
    for m in used:
        key = {"FUTURE_SIGHT": "FUTURESIGHT", "PYRO_BALL": "PYROBALL", "TECHNO_BLAST_DOUSE": "TECHNO_BLAST_WATER"}.get(m, m)
        # PvPoke's own moves take their Portuguese name from the move they stand for.
        key = re.sub(r"^HIDDEN_POWER_[A-Z]+$", "HIDDEN_POWER", re.sub(r"^AEGISLASH_CHARGE_|_PLUS$", "", key))
        move_keys |= {key, key + "_FAST"}
    names = set()
    kept = []
    for template in game_master:
        template_id, data = template["templateId"], template["data"]
        dex = re.match(r"^V(\d{4})_POKEMON_", template_id)
        move = re.match(r"^V\d{4}_MOVE_(.+)$", template_id)
        if "pokemonSettings" in data and dex and int(dex.group(1)) in DEX:
            settings = data["pokemonSettings"]
            names.add(settings["pokemonId"])
            for branch in settings.get("evolutionBranch", []):
                names.update(q["questRequirementTemplateId"] for q in branch.get("questDisplay", []))
            # Only what the job reads (the rest is models, cameras and animations).
            settings = {k: v for k, v in settings.items() if k in POKEMON_FIELDS}
            kept.append({"templateId": template_id, "data": {"templateId": template_id, "pokemonSettings": settings}})
        elif "moveSettings" in data and move and move.group(1) in move_keys:
            kept.append(template)
    for template in game_master:
        data = template["data"]
        if "pokemonExtendedSettings" in data and data["pokemonExtendedSettings"].get("uniqueId") in names:
            extended = data["pokemonExtendedSettings"]
            extended = {k: v for k, v in extended.items() if k in ("uniqueId", "form")} | (
                {"breadOverrides": [{"breadMode": o["breadMode"]} for o in extended["breadOverrides"]]}
                if "breadOverrides" in extended else {})
            kept.append({"templateId": template["templateId"],
                         "data": {"templateId": template["templateId"], "pokemonExtendedSettings": extended}})
        elif "evolutionQuestTemplate" in data and template["templateId"] in names:
            kept.append(template)
    write("GAME_MASTER.json", kept)

    # The game text: type names, the species' names (with their Mega and Gigantamax names), the moves they use by
    # number, and every form text (small, and the names are looked up by several keys).
    numbers = {re.match(r"^V(\d{4})_MOVE_", t["templateId"]).group(1) for t in kept if "moveSettings" in t["data"]}
    names_wanted = re.compile(r"^(pokemon_type_.*|form_.*|pokemon_name_(%s)(_.*)?|move_name_(%s))$"
                              % ("|".join(f"{d:04d}" for d in sorted(DEX)), "|".join(sorted(numbers))))
    for name, out in (("pt-br_raw.json", "game-text-pt-br.json"), ("pt-br_remote_raw.json", "game-text-pt-br-remote.json")):
        pairs = json.loads((source / name).read_text(encoding="utf-8"))["data"]
        kept_text = [x for key, text in zip(pairs[::2], pairs[1::2]) if names_wanted.match(key) for x in (key, text)]
        (OUT / out).write_text(json.dumps({"data": kept_text}, ensure_ascii=False, indent=0) + "\n", encoding="utf-8")
        print(f"{out}: {(OUT / out).stat().st_size:,} bytes")


def write(name, value):
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"{name}: {(OUT / name).stat().st_size:,} bytes")


if __name__ == "__main__":
    main(Path(sys.argv[1]))
