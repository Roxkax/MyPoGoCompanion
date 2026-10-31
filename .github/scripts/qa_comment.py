"""Keeps one "QA" comment per pull request up to date (called by .github/workflows/deploy-qa.yml).

- On the deployed PR: "QA now shows PR #n" with the web app and API links, or that the deploy failed.
- After a successful deploy, on every other open PR that has the comment: "QA has moved on to PR #n", with how to
  bring that PR back.

Reads PR, VERSION, API_URL, WEB_URL, OUTCOME (success / failure), UI_OUTCOME (the UI tests step: success / failure /
skipped), UI_PASSED, UI_FAILED, UI_FLAKY, RUN_URL, WORKFLOW_URL and GITHUB_REPOSITORY from the environment; talks to GitHub with the gh command-line tool (GH_TOKEN).
"""

import json
import os
import subprocess

MARKER = "<!-- qa-deploy -->"


def gh(*args: str, body: str | None = None) -> str:
    command = ["gh", "api", *args]
    if body is not None:
        command += ["-f", f"body={body}"]
    return subprocess.run(command, check=True, capture_output=True, text=True).stdout


def find_comment(repo: str, number: int) -> int | None:
    pages = gh("--paginate", "--slurp", f"repos/{repo}/issues/{number}/comments")
    for page in json.loads(pages):
        for comment in page:
            if MARKER in comment["body"]:
                return comment["id"]
    return None


def upsert(repo: str, number: int, body: str, create: bool = True) -> None:
    comment = find_comment(repo, number)
    if comment is not None:
        gh("-X", "PATCH", f"repos/{repo}/issues/comments/{comment}", body=body)
    elif create:
        gh("-X", "POST", f"repos/{repo}/issues/{number}/comments", body=body)


def ui_tests_line(env: dict[str, str]) -> str:
    outcome = env.get("UI_OUTCOME", "")
    passed, failed, flaky = env.get("UI_PASSED") or "?", env.get("UI_FAILED") or "?", env.get("UI_FLAKY") or "0"
    if outcome == "success":
        retried = f" ({flaky} only on the retry)" if flaky not in ("", "0") else ""
        return f"UI tests: {passed} passed{retried}"
    if outcome == "failure":
        return f"**UI tests: {failed} failed**, {passed} passed"
    return "UI tests didn't run"


def deployed_body(env: dict[str, str]) -> str:
    pr, version, run = env["PR"], env["VERSION"], env["RUN_URL"]
    if env.get("OUTCOME") != "success":
        return (
            f"{MARKER}\n### ⚠️ QA deploy of PR #{pr} failed\n\n"
            f"Commit `{version}`. See [the run]({run}) for the step that failed. "
            "QA may still show the previous PR, or only part of this one.\n"
        )
    web, api = env["WEB_URL"], env["API_URL"]
    ui_ok = env.get("UI_OUTCOME") == "success"
    heading = f"✅ QA now shows PR #{pr}" if ui_ok else f"❌ QA now shows PR #{pr}, but the UI tests didn't pass"
    report = "" if ui_ok else " (summary and report with traces, screenshots and videos in the run)"
    return (
        f"{MARKER}\n### {heading}\n\n"
        f"Commit `{version}` · smoke test passed · {ui_tests_line(env)} · [run]({run}){report}\n\n"
        "| | |\n|---|---|\n"
        f"| Web app | {web} |\n"
        f"| API health | {api}/health |\n"
        f"| API description | {api}/openapi/v1.json |\n\n"
        "The first visit after a quiet spell can take a few seconds while the API and database wake up.\n"
    )


def moved_on_body(env: dict[str, str], branch: str) -> str:
    return (
        f"{MARKER}\n### QA has moved on to PR #{env['PR']}\n\n"
        "QA shows one pull request at a time. To bring this one back: **re-run** the *QA deploy* check on this PR, "
        f"or [Actions → QA deploy → Run workflow]({env['WORKFLOW_URL']}) on branch `{branch}`.\n"
    )


def main() -> None:
    env = dict(os.environ)
    repo, pr = env["GITHUB_REPOSITORY"], int(env["PR"])
    upsert(repo, pr, deployed_body(env))
    if env.get("OUTCOME") != "success":
        return
    others = json.loads(gh(f"repos/{repo}/pulls?state=open&per_page=100"))
    for other in others:
        if other["number"] != pr:
            upsert(repo, other["number"], moved_on_body(env, other["head"]["ref"]), create=False)


if __name__ == "__main__":
    main()
