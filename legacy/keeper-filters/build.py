#!/usr/bin/env python3
"""Fetch the data, rebuild every string and write dist/pvp-keeper-filters.html.

    python build.py              # fetch/update data (git), rebuild everything
    python build.py --no-fetch   # rebuild from the data already in ./data
    python build.py --only pvp|raid|mega|page   # one stage, or several: --only pvp,raid,mega
"""
import argparse
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "scripts"))

SOURCES = {
    "pvpoke": ("https://github.com/pvpoke/pvpoke", [
        "/src/data/gamemaster.json",
        "/src/data/rankings/all/overall/rankings-1500.json",
        "/src/data/rankings/all/overall/rankings-2500.json",
        "/src/js/pokemon/Pokemon.js",
    ]),
    "gm": ("https://github.com/alexelgt/game_masters", ["/GAME_MASTER.json"]),
}

STAGES = {
    "pvp": ["pvp_compute", "pvp_strings", "pvp_page_data"],
    "raid": ["raid_rank", "raid_page_data"],
    "mega": ["mega_page_data"],
    "page": ["page_assemble"],
}


def git(*args, cwd=None):
    subprocess.run(["git", *args], cwd=cwd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)


def fetch():
    data = ROOT / "data"
    data.mkdir(exist_ok=True)
    for name, (url, paths) in SOURCES.items():
        repo = data / name
        if not (repo / ".git").exists():
            print(f"fetch: cloning {url}")
            git("clone", "--depth", "1", "--filter=blob:none", "--sparse", url, str(repo))
        else:
            print(f"fetch: updating {name}")
            git("fetch", "--depth", "1", "origin", cwd=repo)
            git("reset", "--hard", "FETCH_HEAD", cwd=repo)
        git("sparse-checkout", "set", "--no-cone", *paths, cwd=repo)


def stages(value):
    names = [v.strip() for v in value.split(",") if v.strip()]
    unknown = [n for n in names if n not in STAGES]
    if unknown or not names:
        raise argparse.ArgumentTypeError(f"unknown stage {', '.join(unknown) or value!r}; choose from {', '.join(STAGES)}")
    return set(names)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--no-fetch", action="store_true", help="use the data already in ./data")
    ap.add_argument("--only", type=stages, help="run only these stages (comma-separated): " + ", ".join(STAGES))
    args = ap.parse_args()
    t0 = time.time()
    if not args.no_fetch and not args.only:
        fetch()
    for stage, modules in STAGES.items():
        if args.only and stage not in args.only:
            continue
        for mod in modules:
            rc = __import__(mod).main(*([[]] if mod == "pvp_compute" else []))
            if rc:
                return rc
    print(f"build: done in {time.time() - t0:.1f} s")
    return 0


if __name__ == "__main__":
    sys.exit(main())
