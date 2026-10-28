"""Shared paths, data loading and Pokémon GO stat helpers."""
import json
import re
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DATA = ROOT / "data"
BUILD = ROOT / "build"
DIST = ROOT / "dist"
WEB = ROOT / "web"

PVPOKE = DATA / "pvpoke" / "src"
GM_FILE = DATA / "gm" / "GAME_MASTER.json"

MAX_DEX = 1025          # last Pokédex number covered
VERIFY_DEX = 1100       # verification also checks that 1026..1100 never match
MAX_LEN = 1950          # iOS text replacement caps a phrase at 2000 chars
LEAGUES = {"GL": 1500, "UL": 2500}

# Forms that only exist during a battle; a player can never own one.
BATTLE_ONLY = {
    "aegislash_blade", "mimikyu_busted",
    "cramorant_gorging", "cramorant_gulping", "morpeko_hangry",
}

# IV -> appraisal bar (0 = 0, 1 = 1–5, 2 = 6–10, 3 = 11–14, 4 = 15)
def iv_bar(iv):
    if iv == 0:
        return 0
    if iv <= 5:
        return 1
    if iv <= 10:
        return 2
    if iv <= 14:
        return 3
    return 4

BAR_OF_IV = [iv_bar(i) for i in range(16)]
IVS_IN_BAR = [[i for i in range(16) if iv_bar(i) == b] for b in range(5)]


def load_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save_json(obj, path, indent=None):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=indent, separators=None if indent else (",", ":"))


@lru_cache(None)
def pvpoke_gamemaster():
    return load_json(PVPOKE / "data" / "gamemaster.json")


def pvpoke_rankings(cap):
    return load_json(PVPOKE / "data" / "rankings" / "all" / "overall" / f"rankings-{cap}.json")


@lru_cache(None)
def cpm_table():
    """CP multipliers parsed from PvPoke's Pokemon.js; index i = level 1 + i/2."""
    src = (PVPOKE / "js" / "pokemon" / "Pokemon.js").read_text(encoding="utf-8")
    m = re.search(r"var\s+cpms\s*=\s*\[([^\]]+)\]", src)
    if not m:
        raise SystemExit("Could not find the CP multiplier table in Pokemon.js")
    return [float(x) for x in m.group(1).split(",")]


def cpm_at(level):
    return cpm_table()[int(round((level - 1) * 2))]


def is_mega(p):
    sid = p["speciesId"]
    return "mega" in p.get("tags", []) or "_mega" in sid or "_primal" in sid


def is_shadow(p):
    return "shadow" in p.get("tags", []) or p["speciesId"].endswith("_shadow")


def species_by_id():
    return {p["speciesId"]: p for p in pvpoke_gamemaster()["pokemon"]}


def ranges(nums):
    """Sorted ints -> list of (lo, hi) runs."""
    out = []
    for n in sorted(set(nums)):
        if out and n == out[-1][1] + 1:
            out[-1][1] = n
        else:
            out.append([n, n])
    return [tuple(r) for r in out]


def fmt_ranges(runs):
    return ",".join(f"{a}" if a == b else f"{a}-{b}" for a, b in runs)


def data_dates():
    """Timestamps of the two data sources (for the page footer)."""
    import subprocess
    out = {}
    for key, repo in (("pvpoke", DATA / "pvpoke"), ("gamemaster", DATA / "gm")):
        try:
            out[key] = subprocess.run(["git", "-C", str(repo), "log", "-1", "--format=%cs"],
                                      capture_output=True, text=True, check=True).stdout.strip()
        except Exception:
            out[key] = "unknown"
    return out
