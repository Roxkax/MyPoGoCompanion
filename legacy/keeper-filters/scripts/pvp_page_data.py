"""Page rows for the PvP tabs -> build/data_pvp.json.

One row per evolution family (all Pokédex numbers linked by evolution, regional
branches included), each with the box of every member and its own single-family string.
"""
import sys

from common import BUILD, LEAGUES, load_json, save_json
from pvp_compute import species_list
from pvp_strings import build, load_boxes, verify

STAT = ("Atk", "Def", "HP")


def families():
    species = species_list()
    parent = {}

    def find(x):
        while parent.setdefault(x, x) != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        parent[find(a)] = find(b)

    for p in species.values():
        fam = p.get("family", {})
        union(p["dex"], fam.get("id", p["speciesId"]))
        for e in fam.get("evolutions", []):
            if e in species:
                union(p["dex"], species[e]["dex"])
    groups = {}
    for p in species.values():
        groups.setdefault(find(p["dex"]), set()).add(p["dex"])
    names = {}
    for p in sorted(species.values(), key=lambda p: (p["dex"], "_" in p["speciesId"], len(p["speciesId"]))):
        names.setdefault(p["dex"], p["speciesName"])
    return sorted((sorted(g) for g in groups.values()), key=lambda g: g[0]), names


def main():
    fams, names = families()
    meta = load_json(BUILD / "pvp_boxes.json")
    strings = load_json(BUILD / "pvp_strings.json")
    out = {"names": {str(d): n for d, n in names.items()},
           "species": {sid: p["speciesName"] for sid, p in species_list().items()}, "leagues": {}}
    for league, cap in LEAGUES.items():
        boxes = load_boxes(league)
        info = meta["leagues"][league]["boxes"]
        rows = []
        for fam in fams:
            s = build(boxes, fam, cap)
            verify({d: boxes[d] for d in fam}, [s], cap)
            members = []
            for d in fam:
                b = info[str(d)]
                members.append({
                    "dex": d, "box": b["box"], "f1": b["f1"], "keepers": b["keepers"], "tp": b["tp"],
                    "inBox": b["inBox"], "judgedBy": b["sources"], "self": all(x in b["forms"] for x in b["sources"]),
                    "rank1": {k: v["iv"] for k, v in b["rank1"].items()},
                })
            rows.append({"dex": fam, "string": s, "members": members})
        out["leagues"][league] = {"cap": cap, "strings": strings[league]["strings"], "rows": rows}
        print(f"pvp_page_data {league}: {len(rows)} family rows, longest family string "
              f"{max(len(r['string']) for r in rows)} chars")
    save_json(out, BUILD / "data_pvp.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())
