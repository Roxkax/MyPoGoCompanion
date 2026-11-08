"""Writes the Markdown summary of one daily import run (run_import.sh): status, duration, changes per kind, what the
sources no longer list (a held run), the sanity checks and the warnings.

    python3 import_result.py <Azure execution status> '<job_runs row as JSON, or empty>'
"""

import json
import sys
from datetime import datetime


def when(value: str) -> datetime:
    return datetime.fromisoformat(value)


def main(status: str, row_json: str) -> str:
    lines = [f"### Daily import: {status}", ""]
    if not row_json.strip():
        lines.append("No run log found in the database for this run (it may have stopped before it started logging).")
        return "\n".join(lines)
    row = json.loads(row_json)
    summary = row.get("summary") or {}
    seconds = (when(row["finished_at"]) - when(row["started_at"])).total_seconds() if row.get("finished_at") else None
    lines.append(f"- Run log: **{row['status']}**" + (f" in {seconds:.0f} s" if seconds is not None else ""))
    if summary.get("version"):
        lines.append(f"- Job version `{summary['version']}`, started by {summary.get('trigger') or '?'}")
    if row["status"] == "Held":
        lines.append("- **Held: nothing was published.** The sources no longer list the rows below; the super admin "
                     "decides (Run daily import with \"Publish what's missing\" ticked publishes them).")
    if summary.get("publishedMissing"):
        lines.append("- Published what was missing, as the super admin accepted.")
    if row.get("error"):
        lines.append(f"- Why it failed: {row['error']}")
    if summary.get("unchanged"):
        lines.append("- The sources and the job were the same as at the last successful run: nothing to do.")
    changes = summary.get("changes") or {}
    if changes:
        if row["status"] == "Held":
            lines += ["", "What would have changed (nothing was published):"]
        lines += ["", "| Kind | Added | Changed | Removed |", "|---|---:|---:|---:|"]
        for kind, count in changes.items():
            lines.append(f"| {kind} | {count.get('added', 0)} | {count.get('changed', 0)} | {count.get('removed', 0)} |")
    lines += removals_lines(summary.get("removals") or {})
    checks = summary.get("checks") or []
    if checks:
        lines += ["", "| Sanity check | Result |", "|---|---|"]
        for check in checks:
            result = "passed" if check.get("passed") else "**failed**: " + " ".join(check.get("problems") or [])
            detail = f" ({check['detail']})" if check.get("detail") else ""
            lines.append(f"| {check.get('name')} | {result}{detail} |")
    warnings = summary.get("warnings") or []
    if warnings:
        lines += ["", f"**{len(warnings)} warning(s)**", ""] + [f"- {w}" for w in warnings]
    return "\n".join(lines)


def removals_lines(removals: dict) -> list[str]:
    """What the sources no longer list, per kind: how many and the first labels (shared with import_alert.py)."""
    if not removals:
        return []
    total = sum(r.get("count", 0) for r in removals.values())
    lines = ["", f"**No longer in the sources ({total} row{'' if total == 1 else 's'})**", ""]
    for kind, removal in removals.items():
        items = removal.get("items") or []
        more = removal.get("count", 0) - len(items)
        shown = ", ".join(items) + (f" and {more} more" if more > 0 else "")
        lines.append(f"- {KINDS.get(kind, kind)} ({removal.get('count', 0)}): {shown}")
    return lines


# Readable names of the run log's kinds.
KINDS = {
    "forms": "Forms no longer released",
    "movepools": "Moves gone from movepools",
    "evolutions": "Evolutions",
    "cpMultipliers": "CP multipliers",
    "pvpRankings": "PvP rankings",
    "pvpBestIvs": "PvP best IVs",
    "pvpKeeperBoxes": "Keeper boxes",
    "raidRankings": "Raid rankings",
    "raidMovesets": "Raid movesets",
    "costumes": "Costumes",
    "pvpMoveUsage": "PvP move usage",
}


if __name__ == "__main__":
    print(main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else ""))
