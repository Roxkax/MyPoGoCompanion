#!/usr/bin/env python3
"""Builds the backlog from the JSON fragments in this folder.

    python planning/backlog/build.py          # validate, fill sprints, write BACKLOG.md and backlog.out.json

Fragment format (one epic per file, files read in name order):
  {"epic": {"key", "title", "description", "area": [...],},
   "features": [{"key", "title", "tier": "MVP"|"Good to have"|"Future", "items": ["4.1", ...],
                 "screens": ["D1", ...], "area": [...], "description",
                 "tasks": [["title", hours], ...],
                 "tests": [unit, integration, ui]      # hours; expands into test tasks
                 or "no_tests": "reason"}]}
Every feature also gets an "Owner review & testing on QA" task (15% of its hours, at least 1 h).
Sprints hold 100 hours, filled task by task in order (MVP first; Good to have starts a fresh sprint).
Future features are listed but not scheduled.
"""
import json
import math
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SPRINT_HOURS = 100
TIERS = ["MVP", "Good to have", "Future"]
TEST_TASKS = [("Unit tests (xUnit / Vitest)", "testing"),
              ("Integration tests (API + real PostgreSQL)", "testing"),
              ("UI tests (Playwright: phone + desktop, light + dark)", "testing")]


def priorities():
    out = {}
    for line in (ROOT / "planning/PRIORITIES.md").read_text().splitlines():
        m = re.match(r"^\| (\d+\.\d+) \| (.+?) \| \*\*(MVP|Good to have|Future)\*\* \|", line)
        if m:
            out[m.group(1)] = (m.group(3), m.group(2))
    return out


def load():
    epics = []
    for f in sorted(HERE.glob("*.json")):
        if f.name == "backlog.out.json":
            continue
        data = json.loads(f.read_text())
        data["file"] = f.name
        epics.append(data)
    return epics


def expand(feature):
    tasks = [{"title": t[0], "hours": t[1], "area": t[2] if len(t) > 2 else None} for t in feature["tasks"]]
    if "tests" in feature:
        for (title, area), h in zip(TEST_TASKS, feature["tests"]):
            if h:
                tasks.append({"title": title, "hours": h, "area": area})
    base = sum(t["hours"] for t in tasks)
    tasks.append({"title": "Owner review & testing on QA", "hours": max(1, math.ceil(base * 0.15)), "area": "review"})
    for i, t in enumerate(tasks, 1):
        t["key"] = f'{feature["key"]}-T{i:02d}'
    return tasks


def validate(epics, prio):
    errors, seen_items, seen_keys = [], {}, set()
    for e in epics:
        for k in [e["epic"]["key"]] + [f["key"] for f in e["features"]]:
            if k in seen_keys:
                errors.append(f"duplicate key {k}")
            seen_keys.add(k)
        for f in e["features"]:
            if f["tier"] not in TIERS:
                errors.append(f'{f["key"]}: unknown tier {f["tier"]}')
            if f["tier"] != "Future" and "tests" not in f and "no_tests" not in f:
                errors.append(f'{f["key"]}: every MVP / Good to have feature needs "tests" or a "no_tests" reason')
            for it in f.get("items", []):
                if it not in prio:
                    errors.append(f'{f["key"]}: item {it} is not in PRIORITIES.md')
                elif prio[it][0] != f["tier"]:
                    errors.append(f'{f["key"]}: item {it} is {prio[it][0]} in PRIORITIES.md, feature says {f["tier"]}')
                if it in seen_items:
                    errors.append(f"item {it} appears in {seen_items[it]} and {f['key']}")
                seen_items[it] = f["key"]
    for it, (tier, name) in prio.items():
        if it not in seen_items:
            errors.append(f"item {it} ({tier}: {name}) is not in any feature")
    return errors


def schedule(epics):
    """Fill 100-hour sprints task by task, in order (MVP first). Good to have starts in a fresh sprint."""
    order = {t: i for i, t in enumerate(TIERS)}
    feats = []
    for e in epics:
        for f in e["features"]:
            f["epic"] = e["epic"]["key"]
            f["task_list"] = expand(f)
            f["hours"] = sum(t["hours"] for t in f["task_list"])
            feats.append((order[f["tier"]], f))
    feats.sort(key=lambda x: x[0])
    sprint, used, sprints, tier = 0, 0, {}, None
    for _, f in feats:
        if f["tier"] == "Future":
            f["sprint"] = None
            for t in f["task_list"]:
                t["sprint"] = None
            continue
        if tier is not None and f["tier"] != tier and used:
            sprint, used = sprint + 1, 0
        tier = f["tier"]
        for t in f["task_list"]:
            if used and used + t["hours"] > SPRINT_HOURS:
                sprint, used = sprint + 1, 0
            t["sprint"] = sprint
            used += t["hours"]
            sprints[sprint] = used
        f["sprint"] = f["task_list"][0]["sprint"]
        f["sprint_end"] = f["task_list"][-1]["sprint"]
    return sprints


def write(epics, sprints, prio):
    out = {"sprint_hours": SPRINT_HOURS, "sprints": [], "epics": []}
    for s, h in sorted(sprints.items()):
        tiers = sorted({f["tier"] for e in epics for f in e["features"] if f.get("sprint") == s}, key=TIERS.index)
        out["sprints"].append({"number": s, "title": f"Sprint {s}", "hours": h, "tiers": tiers})
    for e in epics:
        ep = dict(e["epic"])
        ep["hours"] = sum(f["hours"] for f in e["features"])
        ep["features"] = [{k: v for k, v in f.items() if k not in ("tasks", "epic")} | {"tasks": f["task_list"]} for f in e["features"]]
        for f in ep["features"]:
            f.pop("task_list", None)
        out["epics"].append(ep)
    (HERE / "backlog.out.json").write_text(json.dumps(out, indent=1, ensure_ascii=False))

    feats = [f for e in out["epics"] for f in e["features"]]
    tot = {t: sum(f["hours"] for f in feats if f["tier"] == t) for t in TIERS}
    tests = {t: sum(x["hours"] for f in feats if f["tier"] == t for x in f["tasks"] if x["area"] in ("testing",)) for t in TIERS}
    ntasks = sum(len(f["tasks"]) for f in feats)
    L = ["# MyPoGoCompanion — backlog", "",
         "Generated by `planning/backlog/build.py` from the JSON fragments in `planning/backlog/`. Do not edit by hand.", "",
         f"**{len(out['epics'])} epics · {len(feats)} features · {ntasks} tasks.** 1 point = 1 hour, including the owner's review and testing time. Sprints hold {SPRINT_HOURS} hours.", "",
         "| Tier | Hours | of which tests | Sprints |", "|---|---|---|---|"]
    for t in TIERS:
        ss = sorted({f["sprint"] for f in feats if f["tier"] == t and f["sprint"] is not None})
        span = f"{ss[0]}–{ss[-1]}" if ss else "not scheduled"
        L.append(f"| {t} | {tot[t]:,} | {tests[t]:,} ({tests[t] * 100 // max(tot[t], 1)}%) | {span} |")
    L += ["", "Every feature has its own unit, integration and UI test tasks (where they apply) plus an owner review & testing task; the **Quality** epic adds end-to-end journeys, visual regression of every screen, accessibility, performance, security and mutation testing.", "",
          "## Sprints", "", "| Sprint | Hours | Features |", "|---|---|---|"]
    for s in out["sprints"]:
        names = [f'{f["key"]} {f["title"]}' for f in feats if f["sprint"] is not None and f["sprint"] <= s["number"] <= f["sprint_end"]]
        L.append(f'| {s["number"]} | {s["hours"]} | {"; ".join(names)} |')
    for e in out["epics"]:
        L += ["", f'## {e["key"]} · {e["title"]} — {e["hours"]} h', "", e.get("description", "")]
        for f in e["features"]:
            sp = ("not scheduled" if f["sprint"] is None else f'Sprint {f["sprint"]}' if f["sprint"] == f["sprint_end"] else f'Sprints {f["sprint"]}–{f["sprint_end"]}')
            meta = " · ".join(x for x in [f["tier"], sp, f'{f["hours"]} h',
                                          ("items " + ", ".join(f.get("items", []))) if f.get("items") else "",
                                          ("screens " + ", ".join(f.get("screens", []))) if f.get("screens") else ""] if x)
            L += ["", f'### {f["key"]} · {f["title"]}', "", f"*{meta}*", ""]
            if f.get("description"):
                L += [f["description"], ""]
            L += [f'- {t["title"]} — {t["hours"]} h' + (f' · S{t["sprint"]}' if t["sprint"] is not None and f["sprint"] != f["sprint_end"] else "") for t in f["tasks"]]
    (ROOT / "planning/BACKLOG.md").write_text("\n".join(L) + "\n")
    return tot, ntasks, len(feats)


def main():
    prio = priorities()
    epics = load()
    errors = validate(epics, prio)
    if errors:
        print("\n".join("error: " + e for e in errors))
        sys.exit(1)
    sprints = schedule(epics)
    tot, ntasks, nfeat = write(epics, sprints, prio)
    print(f"{len(epics)} epics, {nfeat} features, {ntasks} tasks; " + ", ".join(f"{t} {h} h" for t, h in tot.items())
          + f"; {len(sprints)} sprints (0–{max(sprints)})")


if __name__ == "__main__":
    main()
