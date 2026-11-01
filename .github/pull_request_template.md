<!--
One pull request per feature. Close every issue of the feature with its own "Closes #n" line: the feature issue,
each task and its "Owner review & testing" task. Merging the pull request is the owner's approval of the feature.
-->

## What this does

<!-- A few lines from the user's point of view; then what was built behind it. -->

Closes #

## How to test

<!-- Steps the owner follows, and where: on QA (add the `qa` label), locally, or on prod after the merge. -->

1.

## Checklist

- [ ] Every issue of the feature has a `Closes #n` line (feature, tasks, owner review)
- [ ] Branch rebased on the latest `main`, no merge commits (the `rebased` check)
- [ ] Unit tests added or updated; line coverage stays at 90% or more (.NET and web)
- [ ] Integration tests for new endpoints, queries and migrations
- [ ] UI tests for new screens and flows
- [ ] Migrations keep the previous version working (add first, remove in a later pull request)
- [ ] No secrets, keys or personal data in the code, tests or screenshots
- [ ] Docs updated (`README.md`, `docs/`, `planning/`) and **Sprint progress** in `CLAUDE.md`
- [ ] Choices made while building confirmed with the owner and added to the decision log
- [ ] Owner: tested on QA (`qa` label), locally or as the steps above say

## Notes

<!-- Choices made, follow-ups, anything the owner sets up by hand. -->
