"""UI-test results summary for the ui-tests check (called by .github/workflows/deploy-qa.yml).

Reads the JSON report Playwright writes (tests/ui/playwright.config.ts) and prints a Markdown summary: totals, every
failed test with its project (phone/desktop, light/dark) and error, and the tests that only passed on a retry.
With GITHUB_OUTPUT set, also writes passed / failed / flaky / skipped counts for later steps.

  ui_results.py <results.json>
"""

import json
import os
import re
import sys

ANSI = re.compile(r"\x1b\[[0-9;]*m")


def first_lines(text: str, count: int = 8) -> str:
    lines = ANSI.sub("", text).strip().splitlines()
    shown = "\n".join(lines[:count])
    return shown + ("\n…" if len(lines) > count else "")


def walk(suite: dict, path: list[str]):
    """Yields (title path, spec) for every spec in a suite and its child suites."""
    title = suite.get("title", "")
    here = path + [title] if title and not title.endswith(".ts") else path
    for spec in suite.get("specs", []):
        yield here + [spec["title"]], spec
    for child in suite.get("suites", []):
        yield from walk(child, here)


def summarise(report: dict) -> tuple[str, dict[str, int]]:
    failed: list[str] = []
    flaky: list[str] = []
    counts = {"passed": 0, "failed": 0, "flaky": 0, "skipped": 0}

    for suite in report.get("suites", []):
        file = suite.get("file") or suite.get("title", "")
        for titles, spec in walk(suite, []):
            name = " › ".join(titles)
            for test in spec.get("tests", []):
                project = test.get("projectName") or "default"
                status = test.get("status")
                if status == "expected":
                    counts["passed"] += 1
                elif status == "flaky":
                    counts["flaky"] += 1
                    flaky.append(f"- `{project}` · {name} ({file})")
                elif status == "skipped":
                    counts["skipped"] += 1
                else:
                    counts["failed"] += 1
                    results = test.get("results") or [{}]
                    error = (results[-1].get("error") or {}).get("message", "No error message")
                    failed.append(
                        f"<details><summary>❌ <code>{project}</code> · {name} ({file})</summary>\n\n"
                        f"```\n{first_lines(error)}\n```\n</details>"
                    )

    # Errors outside any test (e.g. the config or a spec file that doesn't load) fail the run too.
    for error in report.get("errors", []):
        counts["failed"] += 1
        failed.append(f"```\n{first_lines(error.get('message', str(error)))}\n```")

    total = sum(counts.values())
    icon = "❌" if counts["failed"] else "✅"
    lines = [
        f"### {icon} UI tests on QA",
        "",
        f"{total} tests · **{counts['passed']} passed** · {counts['failed']} failed · "
        f"{counts['flaky']} passed on retry · {counts['skipped']} skipped",
    ]
    if failed:
        lines += ["", "#### Failed", "", *failed, ""]
        lines.append(
            "Each failed test has a trace, a screenshot and a video in the run's **ui-test-report** artifact "
            "(open `index.html`, or drop `trace.zip` on https://trace.playwright.dev)."
        )
    if flaky:
        lines += ["", "#### Passed only on the retry", "", *flaky]
    if total == 0:
        lines += ["", "No tests ran."]
    return "\n".join(lines) + "\n", counts


def main() -> None:
    path = sys.argv[1]
    try:
        with open(path, encoding="utf-8") as file:
            report = json.load(file)
    except FileNotFoundError:
        report = {"errors": [{"message": f"Playwright wrote no report ({path}): the tests didn't run."}]}

    markdown, counts = summarise(report)
    print(markdown)
    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as file:
            for key, value in counts.items():
                file.write(f"{key}={value}\n")


if __name__ == "__main__":
    main()
