"""Tells the owner when a prod deploy fails, and clears the alert once prod is fine again (called by
.github/workflows/deploy-prod.yml).

- A failed run opens one issue "Prod deploy failed" labelled "prod-alert" and assigned to the repository owner (GitHub
  notifies the assignee: email, web and the mobile app). While it stays open, later failures add a comment to it.
- A successful deploy comments on the open alert and closes it.

Reads OUTCOME (failure / success), FAILED_JOBS, COMMIT, RUN_URL, OWNER and GITHUB_REPOSITORY from the environment;
talks to GitHub with the gh command-line tool (GH_TOKEN).
"""

import json
import os
import subprocess

LABEL = "prod-alert"
TITLE = "Prod deploy failed"


def gh(*args: str) -> str:
    return subprocess.run(["gh", *args], check=True, capture_output=True, text=True).stdout


def open_alert(repo: str) -> int | None:
    found = json.loads(
        gh("issue", "list", "-R", repo, "--label", LABEL, "--state", "open", "--json", "number", "--limit", "1")
    )
    return found[0]["number"] if found else None


def failure_text(env: dict[str, str]) -> str:
    jobs = env.get("FAILED_JOBS", "").strip() or "the deploy"
    guide = f"{env.get('GITHUB_SERVER_URL', 'https://github.com')}/{env['GITHUB_REPOSITORY']}/blob/main/docs/PROD-DEPLOY.md#roll-back"
    head = f"Commit `{env['COMMIT'][:7]}`: **{jobs}** failed. [See the run]({env['RUN_URL']}) for the step and its error.\n\n"
    if jobs == "Reset the QA database":
        return head + (
            "Prod itself is deployed and passed the smoke test; only the QA database wasn't reset to a copy of prod. "
            "QA keeps its old data until this works: fix the cause (the error says which secret or setting), then "
            "re-run the failed job from the run page.\n\n"
            "This issue closes itself after the next successful prod deploy."
        )
    return head + (
        "- Prod may still run the previous version, or the new API with the old web app (the smoke test says which).\n"
        "- Fix it with a new pull request, or re-run the failed jobs if the error was temporary.\n"
        f"- To put the previous version back now: [rollback guide]({guide}).\n\n"
        "This issue closes itself after the next successful prod deploy."
    )


def main() -> None:
    env = dict(os.environ)
    repo, outcome = env["GITHUB_REPOSITORY"], env["OUTCOME"]
    number = open_alert(repo)
    if outcome == "failure":
        if number is None:
            gh("label", "create", LABEL, "-R", repo, "--color", "d93f0b", "--force",
               "--description", "Prod deploy failed (opened and closed by the Prod deploy workflow)")
            url = gh("issue", "create", "-R", repo, "--title", TITLE, "--label", LABEL,
                     "--assignee", env["OWNER"], "--body", failure_text(env)).strip()
            print(f"Opened {url}")
        else:
            gh("issue", "comment", str(number), "-R", repo, "--body", "Failed again.\n\n" + failure_text(env))
            print(f"Added the failure to issue #{number}")
    elif number is not None:
        gh("issue", "close", str(number), "-R", repo, "--reason", "completed", "--comment",
           f"Prod is fine again: commit `{env['COMMIT'][:7]}` deployed and passed the smoke test ([run]({env['RUN_URL']})).")
        print(f"Closed issue #{number}")
    else:
        print("No open prod alert.")


if __name__ == "__main__":
    main()
