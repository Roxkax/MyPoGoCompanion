"""Resets the Neon "qa" branch to a fresh copy of its parent "main" (prod), as planning/CI-CD.md asks after every
prod deploy. Called by .github/workflows/deploy-prod.yml; can also be run by hand.

    NEON_API_KEY=… NEON_PROJECT_ID=… python3 .github/scripts/neon_reset_qa.py [--branch qa] [--parent main] [--check]

With --check it changes nothing: it only confirms the key, the project and that the branch's parent is right (used by
the Environment check workflow).

Uses Neon's "reset from parent" (POST …/branches/{qa}/restore with source_branch_id = main), then waits until Neon
reports the operations finished. Everything on qa is replaced by prod's current data; the connection details (host,
user, password) stay the same. Never prints the API key.
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request

API = "https://console.neon.tech/api/v2"
DONE = {"finished", "skipped"}
FAILED = {"failed", "error", "cancelled"}


class NeonError(Exception):
    pass


def call(method: str, path: str, key: str, body: dict | None = None, attempts: int = 6) -> dict:
    data = json.dumps(body).encode() if body is not None else None
    for attempt in range(1, attempts + 1):
        request = urllib.request.Request(
            f"{API}{path}",
            data=data,
            method=method,
            headers={"Authorization": f"Bearer {key}", "Accept": "application/json", "Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                return json.loads(response.read() or b"{}")
        except urllib.error.HTTPError as error:
            detail = error.read().decode(errors="replace")[:500]
            # 423: another operation is still running on the project; 429/5xx: try again shortly.
            if error.code in (423, 429, 500, 502, 503, 504) and attempt < attempts:
                print(f"… Neon answered HTTP {error.code}, trying again ({attempt}/{attempts})")
                time.sleep(10 * attempt)
                continue
            if error.code == 401:
                raise NeonError("Neon refused the API key (HTTP 401): check the NEON_API_KEY secret.") from None
            if error.code == 404:
                raise NeonError(f"Neon found nothing at {path} (HTTP 404): check NEON_PROJECT_ID.") from None
            raise NeonError(f"Neon answered HTTP {error.code} for {method} {path}: {detail}") from None
        except urllib.error.URLError as error:
            if attempt < attempts:
                time.sleep(10 * attempt)
                continue
            raise NeonError(f"Couldn't reach Neon: {error.reason}") from None
    raise NeonError("unreachable")


def wait_for(operations: list[dict], project: str, key: str, timeout: int = 600) -> None:
    pending = {op["id"] for op in operations if op.get("status") not in DONE}
    deadline = time.monotonic() + timeout
    while pending:
        for op_id in sorted(pending):
            status = call("GET", f"/projects/{project}/operations/{op_id}", key)["operation"]["status"]
            if status in DONE:
                pending.discard(op_id)
            elif status in FAILED:
                raise NeonError(f"Neon operation {op_id} ended as '{status}'.")
        if pending:
            if time.monotonic() > deadline:
                raise NeonError(f"Neon operations still running after {timeout} s: {', '.join(sorted(pending))}")
            time.sleep(5)


def reset(project: str, key: str, branch_name: str, parent_name: str, check: bool = False) -> None:
    name = call("GET", f"/projects/{project}", key)["project"]["name"]
    print(f"Neon project: {name} ({project})")
    branches = call("GET", f"/projects/{project}/branches", key)["branches"]
    by_name = {b["name"]: b for b in branches}
    if parent_name not in by_name:
        raise NeonError(f"No Neon branch named '{parent_name}' (found: {', '.join(sorted(by_name))}).")
    if branch_name not in by_name:
        raise NeonError(f"No Neon branch named '{branch_name}' (found: {', '.join(sorted(by_name))}).")
    parent, branch = by_name[parent_name], by_name[branch_name]
    if branch.get("parent_id") != parent["id"]:
        raise NeonError(
            f"Branch '{branch_name}' isn't a child of '{parent_name}', so it can't be reset from it. "
            f"Re-create '{branch_name}' from '{parent_name}' in Neon (docs/SETUP-ENVIRONMENTS.md, step 1)."
        )
    if check:
        print(f"Ready: '{branch_name}' ({branch['id']}) is a child of '{parent_name}' ({parent['id']}), so it can be reset from it.")
        return
    print(f"Resetting Neon branch '{branch_name}' ({branch['id']}) to the latest state of '{parent_name}' ({parent['id']})")
    result = call("POST", f"/projects/{project}/branches/{branch['id']}/restore", key, {"source_branch_id": parent["id"]})
    wait_for(result.get("operations", []), project, key)
    print(f"Done: '{branch_name}' is now a fresh copy of '{parent_name}'.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--branch", default="qa")
    parser.add_argument("--parent", default="main")
    parser.add_argument("--check", action="store_true", help="only check the key, project and branches")
    args = parser.parse_args()
    key, project = os.environ.get("NEON_API_KEY", "").strip(), os.environ.get("NEON_PROJECT_ID", "").strip()
    missing = [name for name, value in (("NEON_API_KEY", key), ("NEON_PROJECT_ID", project)) if not value]
    if missing:
        print(f"::error::{' and '.join(missing)} not set: see docs/SETUP-ENVIRONMENTS.md, step 6.")
        return 1
    try:
        reset(project, key, args.branch, args.parent, args.check)
    except NeonError as error:
        print(f"::error::{'Neon check' if args.check else 'QA database reset'}: {error}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
