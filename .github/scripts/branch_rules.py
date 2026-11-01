"""Checks that the rules GitHub enforces on main are the ones in .github/rulesets/main.json (called by
.github/workflows/branch-rules.yml; setup in docs/BRANCH-RULES.md).

Reads the rules that apply to the branch (GET /repos/{repo}/rules/branches/{branch}, readable with the workflow's own
token) and compares them with the file: every rule type in the file must be enforced, with the same required checks,
"up to date" setting, conversation resolution and number of approvals; a rule enforced on GitHub but not in the file
is reported too, so the file stays the record of what is on. Writes a summary table (Markdown) to stdout and exits 1
when something differs.

Usage: branch_rules.py <ruleset.json> [<live-rules.json>]
The live rules come from the gh command-line tool (GH_TOKEN, GITHUB_REPOSITORY), or from a file for local tests.
"""

import json
import os
import subprocess
import sys

DESCRIBE = {
    "deletion": "The branch can't be deleted",
    "non_fast_forward": "No force pushes",
    "pull_request": "Changes only through pull requests",
    "required_status_checks": "Required checks pass",
    "required_linear_history": "Linear history",
    "required_signatures": "Signed commits",
    "creation": "The branch can't be created",
    "update": "The branch can't be updated",
    "merge_queue": "Merge queue",
    "code_scanning": "Code scanning results",
}


def describe(rule_type: str) -> str:
    return DESCRIBE.get(rule_type, rule_type)


def checks_of(rule: dict) -> set[str]:
    return {check["context"] for check in rule.get("parameters", {}).get("required_status_checks", [])}


def compare(expected: dict, live: list[dict]) -> tuple[list[tuple[str, str]], list[str]]:
    """Returns (rows for the summary table, problems). Each row is (rule, what GitHub enforces)."""
    by_type: dict[str, list[dict]] = {}
    for rule in live:
        by_type.setdefault(rule["type"], []).append(rule)

    rows: list[tuple[str, str]] = []
    problems: list[str] = []
    wanted_types = set()

    for rule in expected["rules"]:
        kind = rule["type"]
        wanted_types.add(kind)
        params = rule.get("parameters", {})
        found = by_type.get(kind, [])
        if not found:
            rows.append((describe(kind), "❌ not enforced"))
            problems.append(f"{describe(kind)}: not enforced on GitHub.")
            continue

        if kind == "required_status_checks":
            want = checks_of(rule)
            have = set().union(*(checks_of(r) for r in found))
            strict = any(r.get("parameters", {}).get("strict_required_status_checks_policy") for r in found)
            rows.append((describe(kind), ", ".join(sorted(have)) or "none"))
            if want - have:
                problems.append(f"Required checks missing on GitHub: {', '.join(sorted(want - have))}.")
            if have - want:
                problems.append(f"Required on GitHub but not in the file: {', '.join(sorted(have - want))}.")
            rows.append(("Branch up to date before merging", "✅" if strict else "❌ off"))
            if params.get("strict_required_status_checks_policy") and not strict:
                problems.append("Branch up to date before merging: off on GitHub.")
        elif kind == "pull_request":
            resolved = any(r.get("parameters", {}).get("required_review_thread_resolution") for r in found)
            approvals = max(r.get("parameters", {}).get("required_approving_review_count", 0) for r in found)
            rows.append((describe(kind), "✅"))
            rows.append(("Conversations resolved before merging", "✅" if resolved else "❌ off"))
            rows.append(("Approvals required", str(approvals)))
            if "allowed_merge_methods" in params:
                methods = set()
                for r in found:
                    methods |= set(r.get("parameters", {}).get("allowed_merge_methods", []))
                want_methods = set(params["allowed_merge_methods"])
                rows.append(("Merge methods allowed", ", ".join(sorted(methods)) or "none"))
                if methods != want_methods:
                    problems.append(
                        f"Merge methods allowed: {', '.join(sorted(methods))} on GitHub,"
                        f" {', '.join(sorted(want_methods))} in the file."
                    )
            if params.get("required_review_thread_resolution") and not resolved:
                problems.append("Conversations resolved before merging: off on GitHub.")
            if approvals != params.get("required_approving_review_count", 0):
                problems.append(
                    f"Approvals required: {approvals} on GitHub, {params.get('required_approving_review_count', 0)}"
                    " in the file."
                )
        else:
            rows.append((describe(kind), "✅"))

    for kind in sorted(set(by_type) - wanted_types):
        rows.append((describe(kind), "⚠️ enforced, not in the file"))
        problems.append(f"{describe(kind)}: enforced on GitHub but not in the file (add it or remove it).")

    return rows, problems


def live_rules(repo: str, branch: str) -> list[dict]:
    out = subprocess.run(
        ["gh", "api", "--paginate", "--slurp", f"repos/{repo}/rules/branches/{branch}"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    return [rule for page in json.loads(out) for rule in page]


def main() -> int:
    if len(sys.argv) not in (2, 3):
        print(__doc__, file=sys.stderr)
        return 2
    with open(sys.argv[1], encoding="utf-8") as f:
        expected = json.load(f)
    if len(sys.argv) == 3:
        with open(sys.argv[2], encoding="utf-8") as f:
            live = json.load(f)
    else:
        live = live_rules(os.environ["GITHUB_REPOSITORY"], os.environ.get("BRANCH", "main"))

    rows, problems = compare(expected, live)
    print("## Branch rules on `main`\n")
    print("| Rule | On GitHub |")
    print("|---|---|")
    for rule, state in rows:
        print(f"| {rule} | {state} |")
    print()
    if problems:
        print("**Not as in `.github/rulesets/main.json`:**\n")
        for problem in problems:
            print(f"- {problem}")
        print("\nImport the file again (docs/BRANCH-RULES.md) or update it to match.")
        return 1
    print("✅ GitHub enforces the rules in `.github/rulesets/main.json`.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
