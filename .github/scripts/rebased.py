"""Checks that a pull request's branch is rebased on the latest main (required check "rebased", called by
.github/workflows/rebased.yml; rules in docs/BRANCH-RULES.md).

PRs are merged with a merge commit (owner, 2026-11-01), and before that the branch must be a straight line of commits
on top of the latest main: main's newest commit is in the branch, and the branch has no merge commits of its own (no
"Update branch" merges, no "git merge main"). Reads GitHub's comparison of main with the PR's head commit
(GET /repos/{repo}/compare/{base}...{head}); writes a summary (Markdown) to stdout and exits 1 when the branch isn't
rebased.

Usage: rebased.py [<compare.json>]
Without a file, asks GitHub with the gh command-line tool (GH_TOKEN, GITHUB_REPOSITORY, BASE_REF, HEAD_SHA).
"""

import json
import os
import subprocess
import sys

HOW_TO = (
    "Rebase the branch on the latest `main`: on the pull request, **Update branch ▾ → Update with rebase**, "
    "or locally `git fetch origin && git rebase origin/main && git push --force-with-lease`. "
    "For a Dependabot pull request, comment `@dependabot rebase`."
)


def compare(repo: str, base: str, head: str) -> dict:
    out = subprocess.run(
        ["gh", "api", "--paginate", "--slurp", f"repos/{repo}/compare/{base}...{head}?per_page=100"],
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    pages = json.loads(out)
    first = dict(pages[0])
    first["commits"] = [commit for page in pages for commit in page.get("commits", [])]
    return first


def check(data: dict, base_name: str = "main") -> tuple[list[str], list[str]]:
    """Returns (summary lines, problems)."""
    commits = data.get("commits", [])
    behind = data.get("behind_by", 0)
    merges = [c for c in commits if len(c.get("parents", [])) > 1]
    lines = [
        f"- Commits on the branch: {data.get('ahead_by', len(commits))}",
        f"- Behind `{base_name}`: {behind}",
        f"- Merge commits on the branch: {len(merges)}",
    ]
    problems = []
    if behind:
        problems.append(f"The branch is {behind} commit(s) behind `{base_name}`.")
    for c in merges:
        title = c.get("commit", {}).get("message", "").splitlines()[0] if c.get("commit") else ""
        problems.append(f"Merge commit `{c['sha'][:7]}` on the branch: {title}")
    if data.get("total_commits", len(commits)) > len(commits):
        problems.append(
            f"GitHub listed only {len(commits)} of {data['total_commits']} commits, so the rest couldn't be checked."
        )
    return lines, problems


def main() -> int:
    if len(sys.argv) == 2:
        with open(sys.argv[1], encoding="utf-8") as f:
            data = json.load(f)
        base_name = "main"
    elif len(sys.argv) == 1:
        base_name = os.environ.get("BASE_REF", "main")
        data = compare(os.environ["GITHUB_REPOSITORY"], base_name, os.environ["HEAD_SHA"])
    else:
        print(__doc__, file=sys.stderr)
        return 2

    lines, problems = check(data, base_name)
    print(f"## Rebased on `{base_name}`?\n")
    print("\n".join(lines))
    print()
    if problems:
        print("❌ **Not rebased on the latest `" + base_name + "`:**\n")
        for problem in problems:
            print(f"- {problem}")
        print(f"\n{HOW_TO}")
        return 1
    print(f"✅ A straight line of commits on top of the latest `{base_name}`.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
