"""Tells the owner when prod's daily import needs them, and clears the alert once an import is published again
(called by the "Run daily import" workflow, prod's 06:00 UTC schedule included, and by the prod deploy).

- A held run (the sources no longer list some rows, nothing was published) opens one issue "Daily import needs your
  decision", labelled "import-alert" and assigned to the repository owner (GitHub notifies the assignee: email, web
  and the mobile app), listing what would go and how to decide. A failed run opens "Daily import failed". While an
  alert is open, later held or failed runs add a comment to it.
- A published run (or one with nothing to do) comments on the open alert and closes it.
- A run that did nothing because another one was running changes nothing here.

Reads RESULT (succeeded / held / failed / skipped, from run_import.sh), RUN_FILE (the run log row as JSON, may be
empty), RUN_URL, WORKFLOW_URL, OWNER and GITHUB_REPOSITORY from the environment; talks to GitHub with the gh
command-line tool (GH_TOKEN). Decision 2026-11-02 (docs/DAILY-IMPORT.md).
"""

import json
import os
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from import_result import removals_lines  # noqa: E402

LABEL = "import-alert"
HELD_TITLE = "Daily import needs your decision"
FAILED_TITLE = "Daily import failed"


def gh(*args: str) -> str:
    return subprocess.run(["gh", *args], check=True, capture_output=True, text=True).stdout


def open_alert(repo: str) -> int | None:
    found = json.loads(
        gh("issue", "list", "-R", repo, "--label", LABEL, "--state", "open", "--json", "number", "--limit", "1")
    )
    return found[0]["number"] if found else None


def read_run(path: str) -> dict:
    try:
        text = Path(path).read_text(encoding="utf-8").strip() if path else ""
        return json.loads(text) if text else {}
    except (OSError, json.JSONDecodeError):
        return {}


def held_text(run: dict, env: dict[str, str]) -> str:
    summary = run.get("summary") or {}
    lines = [
        f"Prod's daily import ([run]({env['RUN_URL']})) found rows the sources no longer list, so **it published nothing**: "
        "yesterday's data stays live.",
        *removals_lines(summary.get("removals") or {}),
        "",
        "**To publish them** (they are removed or, for forms, marked not released): "
        f"[Run daily import]({env['WORKFLOW_URL']}) → Run workflow → `prod`, tick **Publish what's missing**. "
        "It publishes only these rows; if the list has changed by then, it holds again with the new list.",
        "",
        "**To keep them**: do nothing. The next morning's run holds again while they are missing (and comments here); "
        "after 3 days without a published import the app shows its out-of-date warning on the game-data screens.",
        "",
        "This issue closes itself after the next published import.",
    ]
    for warning in summary.get("warnings") or []:
        if "Publish what's missing" in warning:
            lines.insert(1, f"\n> {warning}\n")
    return "\n".join(lines)


def failed_text(run: dict, env: dict[str, str]) -> str:
    error = run.get("error") or "The run didn't finish or left no run log; the job's console logs are in the Azure portal."
    return (
        f"Prod's daily import failed ([run]({env['RUN_URL']})), so it published nothing: yesterday's data stays live.\n\n"
        f"> {error}\n\n"
        "Broken data can't be published: fix the cause (or wait for the sources to be fixed), then run "
        f"[Run daily import]({env['WORKFLOW_URL']}) again, or let the next morning's run try.\n\n"
        "This issue closes itself after the next published import."
    )


def main() -> None:
    env = dict(os.environ)
    repo, result = env["GITHUB_REPOSITORY"], env.get("RESULT", "failed")
    if result == "skipped":
        print("Another run was importing: nothing to say.")
        return

    run = read_run(env.get("RUN_FILE", ""))
    number = open_alert(repo)
    if result in ("held", "failed"):
        title, body = (HELD_TITLE, held_text(run, env)) if result == "held" else (FAILED_TITLE, failed_text(run, env))
        if number is None:
            gh("label", "create", LABEL, "-R", repo, "--color", "d93f0b", "--force",
               "--description", "The daily import needs the owner (opened and closed by the Run daily import workflow)")
            url = gh("issue", "create", "-R", repo, "--title", title, "--label", LABEL,
                     "--assignee", env["OWNER"], "--body", body).strip()
            print(f"Opened {url}")
        else:
            gh("issue", "comment", str(number), "-R", repo, "--body", f"**{title}** again.\n\n{body}")
            print(f"Added the run to issue #{number}")
    elif number is not None:
        summary = run.get("summary") or {}
        what = "published what was missing, as you accepted" if summary.get("publishedMissing") else "published"
        gh("issue", "close", str(number), "-R", repo, "--reason", "completed", "--comment",
           f"Prod's daily import {what} ([run]({env['RUN_URL']})).")
        print(f"Closed issue #{number}")
    else:
        print("No open import alert.")


if __name__ == "__main__":
    main()
