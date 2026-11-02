"""Writes the Markdown summary of one daily import run (run_import.sh): status, duration, changes per kind, warnings.

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
    if row.get("error"):
        lines.append(f"- Why it failed: {row['error']}")
    if summary.get("unchanged"):
        lines.append("- The sources and the job were the same as at the last successful run: nothing to do.")
    changes = summary.get("changes") or {}
    if changes:
        lines += ["", "| Kind | Added | Changed | Removed |", "|---|---:|---:|---:|"]
        for kind, count in changes.items():
            lines.append(f"| {kind} | {count.get('added', 0)} | {count.get('changed', 0)} | {count.get('removed', 0)} |")
    warnings = summary.get("warnings") or []
    if warnings:
        lines += ["", f"**{len(warnings)} warning(s)**", ""] + [f"- {w}" for w in warnings]
    return "\n".join(lines)


if __name__ == "__main__":
    print(main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else ""))
