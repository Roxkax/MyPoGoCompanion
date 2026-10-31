"""Test-results summary for the integration check.

Reads TRX files (written by xUnit v3 with --report-xunit-trx) and prints a Markdown summary:
totals, every failed test with its error, and a collapsed list of all tests.

  test_results.py <title> <file.trx> [<file.trx> ...]
"""

import sys
import xml.etree.ElementTree as ElementTree

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
ICONS = {"Passed": "✅", "Failed": "❌", "NotExecuted": "⏭️"}


def seconds(duration: str | None) -> float:
    # TRX durations look like 00:00:01.2345678.
    if not duration:
        return 0.0
    hours, minutes, rest = duration.split(":")
    return int(hours) * 3600 + int(minutes) * 60 + float(rest)


def short_name(name: str) -> str:
    # "Namespace.Class.Method(args)" -> "Class.Method(args)"; arguments may contain dots.
    method, bracket, args = name.partition("(")
    return ".".join(method.split(".")[-2:]) + bracket + args


def first_lines(text: str, count: int = 8) -> str:
    lines = text.strip().splitlines()
    return "\n".join(lines[:count] + (["…"] if len(lines) > count else []))


def load(paths: list[str]) -> list[dict]:
    results = []
    for path in paths:
        root = ElementTree.parse(path).getroot()
        for result in root.iterfind(".//t:UnitTestResult", NS):
            message = result.findtext(".//t:ErrorInfo/t:Message", "", NS)
            stack = result.findtext(".//t:ErrorInfo/t:StackTrace", "", NS)
            results.append({
                "name": short_name(result.get("testName", "?")),
                "outcome": result.get("outcome", "?"),
                "seconds": seconds(result.get("duration")),
                "error": "\n".join(part for part in (message, first_lines(stack)) if part.strip()),
            })
    return sorted(results, key=lambda r: r["name"])


def summary(title: str, paths: list[str]) -> str:
    results = load(paths)
    if not results:
        return f"### {title}\n\n❌ No test results were found: the tests did not run (see the job log).\n"

    count = lambda outcome: sum(r["outcome"] == outcome for r in results)
    failed, passed, skipped = count("Failed"), count("Passed"), count("NotExecuted")
    total_seconds = sum(r["seconds"] for r in results)
    status = "❌" if failed else "✅"
    lines = [
        f"### {title}",
        "",
        f"{status} **{passed} passed**, {failed} failed, {skipped} skipped · {len(results)} tests in {total_seconds:.1f} s",
        "",
    ]
    for result in (r for r in results if r["outcome"] == "Failed"):
        lines += [f"<details open><summary>❌ <code>{result['name']}</code></summary>", "", "```", result["error"] or "(no message)", "```", "</details>", ""]
    lines += ["<details><summary>All tests</summary>", "", "| | Test | Time |", "|---|---|---|"]
    lines += [f"| {ICONS.get(r['outcome'], r['outcome'])} | `{r['name']}` | {r['seconds']:.2f} s |" for r in results]
    lines += ["", "</details>", ""]
    return "\n".join(lines)


if __name__ == "__main__":
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    print(summary(sys.argv[1], sys.argv[2:]))
