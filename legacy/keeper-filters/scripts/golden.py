"""Golden files for the C# port: freeze the Python outputs, or check they still reproduce.

    python scripts/golden.py freeze   # after `python build.py`: freeze ./data and the outputs
    python scripts/golden.py check    # rebuild from the frozen inputs and compare (no network)

Everything goes to tests/golden/keeper-filters/ (see the README there):
- inputs/   the source data the outputs were built from, trimmed to what the pipeline reads
            and laid out like ./data, so the Python build and the C# port read the same files;
- outputs/  pvp_boxes, pvp_strings, raiders, data_pvp, data_raid, data_mega (the build/ JSON)
            and searchsim (search-simulator cases);
- manifest.json  source commits, file sizes and SHA-256 hashes.

`freeze` builds twice, from ./data and from the trimmed inputs, and stops if they differ,
so the trimmed inputs are proven to be enough.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np

SCRIPTS = Path(__file__).resolve().parent
KEEPER = SCRIPTS.parent
REPO = KEEPER.parent.parent
GOLDEN = REPO / "tests" / "golden" / "keeper-filters"
INPUTS = GOLDEN / "inputs"
OUTPUTS = GOLDEN / "outputs"
DATA = KEEPER / "data"

sys.path.insert(0, str(SCRIPTS))
from searchsim import evaluate  # noqa: E402

STAGES = "pvp,raid,mega"  # the page stage only assembles HTML and stamps today's date
OUTPUT_FILES = ["pvp_boxes", "pvp_strings", "raiders", "data_pvp", "data_raid", "data_mega"]
SOURCES = {"pvpoke": "https://github.com/pvpoke/pvpoke", "gm": "https://github.com/alexelgt/game_masters"}
GM_MOVE = re.compile(r"^V\d{4}_MOVE_")
GM_POKEMON = re.compile(r"^V\d{4}_POKEMON_")
MAX_DIFFS = 20

# Search-simulator cases: a small universe, every expression evaluated on all of it.
SIM_DEX = [1, 2, 3, 4, 147, 148, 149, 150, 485, 486, 1025, 1026, 1100]
SIM_BARS = [[0, 0, 0], [1, 2, 3], [2, 4, 1], [3, 3, 3], [3, 4, 0], [4, 0, 2], [0, 4, 4], [4, 4, 4]]
SIM_CP = [10, 1500, 1501, 2500, 2501]
SIM_FLAGS = {"maxmove1-": [3, 149, 1025], "mega0-": [3, 150]}
SIM_EXPRESSIONS = [
    "147", "147-149", "-3", "1025-", "!147-149", "147,149", "147;149:3", " 147 , 149 ",
    "4attack", "3-4attack", "-1attack", "3-attack", "0-1defense", "2hp", "!4hp", "3-4attack,4defense",
    "cp-1500", "cp1500-", "cp1501", "!cp-2500", "cp1501-2500",
    "147&4attack", "147|4attack", "1-3&3-4attack,4defense&cp-1500", "!1-3&!4attack", "1-150&3-4attack&cp-1500",
    "maxmove1-", "maxmove1-&4attack", "!maxmove1-", "mega0-", "mega0-,149", "!mega0-&1-4",
]
SIM_ERRORS = ["4*", "shiny", "4attack&shadow", "147-149x", "cp"]


# ---------------------------------------------------------------- inputs

def trim_inputs(src, dest):
    """Copy the parts of ./data the pipeline reads into `dest`, in the same layout."""
    if dest.exists():
        shutil.rmtree(dest)
    pv_src, pv_dest = src / "pvpoke" / "src", dest / "pvpoke" / "src"
    # PvPoke game master and the CP multiplier source: used in full
    for rel in ("data/gamemaster.json", "js/pokemon/Pokemon.js"):
        (pv_dest / rel).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(pv_src / rel, pv_dest / rel)
    # Rankings: only the order of speciesId is read (PvPoke's top 150 per league)
    for cap in (1500, 2500):
        rel = Path("data/rankings/all/overall") / f"rankings-{cap}.json"
        rows = json.loads((pv_src / rel).read_text(encoding="utf-8"))
        write_json([{"speciesId": r["speciesId"]} for r in rows], pv_dest / rel, compact=True)
    # Niantic game master (~19 MB): only the move templates and the Pokémon templates that
    # carry Mega/Primal data are read; the kept templates are unchanged
    gm = json.loads((src / "gm" / "GAME_MASTER.json").read_text(encoding="utf-8"))
    kept = [t for t in gm
            if (GM_MOVE.match(t["templateId"]) and "moveSettings" in t["data"])
            or (GM_POKEMON.match(t["templateId"]) and t["data"].get("pokemonSettings", {}).get("tempEvoOverrides"))]
    write_json(kept, dest / "gm" / "GAME_MASTER.json", compact=True)


def source_commits(src):
    out = {}
    for name, url in SOURCES.items():
        repo = src / name
        commit, date = subprocess.run(["git", "-C", str(repo), "log", "-1", "--format=%H %cs"],
                                      capture_output=True, text=True, check=True).stdout.split()
        out[name] = {"url": url, "commit": commit, "date": date}
    return out


# ---------------------------------------------------------------- outputs

def run_build(data, build):
    env = dict(os.environ, KEEPER_DATA=str(data), KEEPER_BUILD=str(build))
    res = subprocess.run([sys.executable, str(KEEPER / "build.py"), "--no-fetch", "--only", STAGES],
                         env=env, capture_output=True, text=True)
    if res.returncode:
        sys.stdout.write(res.stdout)
        sys.stderr.write(res.stderr)
        raise SystemExit(f"golden: the Python build failed (exit {res.returncode})")
    return res.stdout


def read_outputs(build):
    return {name: json.loads((build / f"{name}.json").read_text(encoding="utf-8")) for name in OUTPUT_FILES}


def searchsim_cases(pvp_strings, data_raid, data_mega):
    """Every expression evaluated on a small universe; matches listed as [dex, atk, def, hp, cp]."""
    dex = np.array(SIM_DEX)[:, None, None]
    bars = np.array(SIM_BARS)[None, :, None, :]
    cp = np.array(SIM_CP)[None, None, :]
    flags = {k: np.isin(dex, v) for k, v in SIM_FLAGS.items()}
    exprs = list(SIM_EXPRESSIONS)
    exprs += [s["text"] for lg in ("GL", "UL") for s in pvp_strings[lg]["strings"][:1]]
    exprs += [data_raid["raid"]["11-15"], data_raid["max"]["15"], data_mega["string"]]
    exprs = list(dict.fromkeys(exprs))
    shape = (len(SIM_DEX), len(SIM_BARS), len(SIM_CP))
    cases = []
    for e in exprs:
        mask = np.broadcast_to(evaluate(e, dex, bars, cp, flags), shape)
        matches = [[SIM_DEX[i], *SIM_BARS[j], SIM_CP[k]] for i, j, k in np.argwhere(mask)]
        cases.append({"expression": e, "matchCount": len(matches), "matches": matches})
    errors = []
    for e in SIM_ERRORS:
        try:
            evaluate(e, dex, bars, cp, flags)
        except ValueError:
            errors.append(e)
        else:
            raise SystemExit(f"golden: expected {e!r} to be rejected by the simulator")
    return {
        "universe": {"dex": SIM_DEX, "bars": SIM_BARS, "cp": SIM_CP, "flags": SIM_FLAGS},
        "cases": cases,
        "unsupported": errors,
    }


def all_outputs(build):
    out = read_outputs(build)
    out["searchsim"] = searchsim_cases(out["pvp_strings"], out["data_raid"], out["data_mega"])
    return out


# ---------------------------------------------------------------- files and diffs

def write_json(obj, path, compact=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(obj, ensure_ascii=False, separators=(",", ":")) if compact else pretty(obj)
    path.write_text(text + "\n", encoding="utf-8", newline="\n")


def pretty(obj, indent=""):
    """Indented JSON with lists of plain values kept on one line, for short, readable diffs."""
    inner = indent + " "
    if isinstance(obj, dict) and obj:
        items = [f"{inner}{json.dumps(k, ensure_ascii=False)}: {pretty(v, inner)}" for k, v in obj.items()]
        return "{\n" + ",\n".join(items) + "\n" + indent + "}"
    if isinstance(obj, list) and obj and any(isinstance(v, (dict, list)) for v in obj):
        if all(isinstance(v, list) and not any(isinstance(x, (dict, list)) for x in v) for v in obj) \
                and len(obj) <= 8:
            return json.dumps(obj, ensure_ascii=False, separators=(", ", ": "))
        return "[\n" + ",\n".join(inner + pretty(v, inner) for v in obj) + "\n" + indent + "]"
    return json.dumps(obj, ensure_ascii=False, separators=(", ", ": "))


def file_info(path):
    data = path.read_bytes()
    return {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}


def diff(expected, actual, path="$", out=None):
    """Readable differences between two JSON values (exact; ints and floats compare by value)."""
    out = [] if out is None else out
    if len(out) >= MAX_DIFFS:
        return out
    if isinstance(expected, dict) and isinstance(actual, dict):
        for k in expected:
            if k not in actual:
                out.append(f"{path}.{k}: missing (expected {short(expected[k])})")
        for k in actual:
            if k not in expected:
                out.append(f"{path}.{k}: unexpected {short(actual[k])}")
        for k in expected:
            if k in actual:
                diff(expected[k], actual[k], f"{path}.{k}", out)
    elif isinstance(expected, list) and isinstance(actual, list):
        if len(expected) != len(actual):
            out.append(f"{path}: {len(expected)} items expected, {len(actual)} found")
        for i, (e, a) in enumerate(zip(expected, actual)):
            diff(e, a, f"{path}[{i}]", out)
    elif type(expected) is not type(actual) and not (is_number(expected) and is_number(actual)):
        out.append(f"{path}: expected {short(expected)}, found {short(actual)}")
    elif expected != actual:
        if isinstance(expected, str) and isinstance(actual, str):
            i = next((n for n, (x, y) in enumerate(zip(expected, actual)) if x != y), min(len(expected), len(actual)))
            out.append(f"{path}: strings differ at character {i}\n"
                       f"    expected …{expected[max(0, i - 30):i + 30]}…\n"
                       f"    found    …{actual[max(0, i - 30):i + 30]}…")
        else:
            out.append(f"{path}: expected {short(expected)}, found {short(actual)}")
    return out


def is_number(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def short(v):
    s = json.dumps(v, ensure_ascii=False)
    return s if len(s) <= 80 else s[:77] + "…"


# ---------------------------------------------------------------- commands

def freeze():
    if not (DATA / "gm" / "GAME_MASTER.json").exists():
        raise SystemExit("golden: no data in ./data; run `python build.py` first")
    with tempfile.TemporaryDirectory() as tmp:
        tmp = Path(tmp)
        print("golden: building from ./data")
        full = all_outputs(build_into(DATA, tmp / "full"))
        print("golden: trimming the inputs")
        trim_inputs(DATA, tmp / "inputs")
        print("golden: building from the trimmed inputs")
        trimmed = all_outputs(build_into(tmp / "inputs", tmp / "trimmed"))
        problems = {name: diff(full[name], trimmed[name]) for name in full}
        problems = {k: v for k, v in problems.items() if v}
        if problems:
            report(problems)
            raise SystemExit("golden: the trimmed inputs give different outputs; keep more of the data")
        if INPUTS.exists():
            shutil.rmtree(INPUTS)
        if OUTPUTS.exists():
            shutil.rmtree(OUTPUTS)
        shutil.copytree(tmp / "inputs", INPUTS)
    for name, obj in full.items():
        write_json(obj, OUTPUTS / f"{name}.json")
    files = sorted(p for p in GOLDEN.rglob("*") if p.is_file() and p.parent != GOLDEN)
    manifest = {
        "sources": source_commits(DATA),
        "python": {"version": sys.version.split()[0], "numpy": np.__version__},
        "files": {p.relative_to(GOLDEN).as_posix(): file_info(p) for p in files},
    }
    write_json(manifest, GOLDEN / "manifest.json")
    total = sum(f["bytes"] for f in manifest["files"].values())
    print(f"golden: froze {len(files)} files ({total / 1024 / 1024:.1f} MB) in {GOLDEN.relative_to(REPO)}")
    for name, src in manifest["sources"].items():
        print(f"  {name}: {src['commit'][:12]} ({src['date']})")
    return 0


def check():
    manifest = json.loads((GOLDEN / "manifest.json").read_text(encoding="utf-8"))
    changed = [rel for rel, info in manifest["files"].items()
               if not (GOLDEN / rel).exists() or file_info(GOLDEN / rel) != info]
    if changed:
        raise SystemExit("golden: these files don't match manifest.json (run `freeze` to refresh): " + ", ".join(changed))
    with tempfile.TemporaryDirectory() as tmp:
        actual = all_outputs(build_into(INPUTS, Path(tmp)))
    problems = {}
    for name, obj in actual.items():
        expected = json.loads((OUTPUTS / f"{name}.json").read_text(encoding="utf-8"))
        d = diff(expected, obj)
        if d:
            problems[name] = d
    if problems:
        report(problems)
        print("golden: the Python build no longer reproduces the golden files. If the change is intended, "
              "run `python build.py` and `python scripts/golden.py freeze` and commit the result.")
        return 1
    print(f"golden: the Python build reproduces all {len(actual)} golden outputs")
    return 0


def build_into(data, build):
    for line in run_build(data, build).splitlines():
        print("  " + line)
    return build


def report(problems):
    for name, lines in problems.items():
        print(f"golden: outputs/{name}.json differs ({len(lines)}{'+' if len(lines) >= MAX_DIFFS else ''} differences)")
        for line in lines:
            print("  " + line)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["freeze", "check"])
    args = ap.parse_args(argv)
    return freeze() if args.command == "freeze" else check()


if __name__ == "__main__":
    sys.exit(main())
