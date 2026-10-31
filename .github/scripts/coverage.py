"""Line-coverage checks and the PR summary for the build-and-unit check.

Reads ReportGenerator JsonSummary files (Summary.json).

  coverage.py check <Summary.json> <minimum %> <label>
      Fails when line coverage is below the minimum.
  coverage.py summary <minimum %> <label>=<Summary.json> [<label>=<Summary.json> ...]
      Prints a Markdown table, one row per label, with a per-class breakdown.
"""

import json
import os
import sys


def load(path: str) -> dict | None:
    if not os.path.exists(path):
        return None
    with open(path, encoding="utf-8") as file:
        return json.load(file)


def percent(value: float | None) -> str:
    return "–" if value is None else f"{value:.1f}%"


def check(path: str, minimum: float, label: str) -> int:
    summary = load(path)["summary"]
    covered = summary.get("linecoverage") or 0.0
    detail = f"{summary.get('coveredlines', 0)} of {summary.get('coverablelines', 0)} lines"
    if covered < minimum:
        print(f"::error title={label} coverage too low::{label} line coverage is {covered:.1f}% ({detail}); the minimum is {minimum:g}%.")
        return 1
    print(f"{label} line coverage is {covered:.1f}% ({detail}); the minimum is {minimum:g}%.")
    return 0


def summary(minimum: float, entries: list[str]) -> int:
    rows, details = [], []
    for entry in entries:
        label, path = entry.split("=", 1)
        report = load(path)
        if report is None:
            rows.append(f"| {label} | – | – | – | ❌ no coverage (the job failed before the tests finished) |")
            continue
        s = report["summary"]
        line = s.get("linecoverage") or 0.0
        status = "✅" if line >= minimum else f"❌ below {minimum:g}%"
        rows.append(
            f"| {label} | **{percent(line)}** | {percent(s.get('branchcoverage'))} "
            f"| {s.get('coveredlines', 0)} / {s.get('coverablelines', 0)} | {status} |"
        )
        classes = [c for a in report["coverage"]["assemblies"] for c in a["classesinassembly"]]
        if classes:
            details.append(f"<details><summary>{label}: by file or class</summary>\n")
            details.append("| Name | Lines | Branches |\n|:---|---:|---:|")
            for c in sorted(classes, key=lambda c: (c.get("coverage") or 0, c["name"])):
                details.append(f"| `{c['name']}` | {percent(c.get('coverage'))} | {percent(c.get('branchcoverage'))} |")
            details.append("\n</details>\n")

    print(f"### Unit-test coverage\n\nMinimum line coverage: **{minimum:g}%** for each part.\n")
    print("| Part | Lines | Branches | Covered lines | Status |\n|:---|---:|---:|---:|:---|")
    print("\n".join(rows))
    print()
    print("\n".join(details))
    return 0


def main() -> int:
    command, args = sys.argv[1], sys.argv[2:]
    if command == "check":
        return check(args[0], float(args[1]), args[2])
    if command == "summary":
        return summary(float(args[0]), args[1:])
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main())
