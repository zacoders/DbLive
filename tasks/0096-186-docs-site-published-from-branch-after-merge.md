---
type: chore
priority: medium
created: 2026-11-25
---
# The docs site is still published from `96-documentation` after the merge

## Problem
`.github/workflows/docs.yml:8` publishes the site on a push to `96-documentation` as well as to `main`. The maintainer decided on 2026-11-13 that this is fine until the merge: nothing links to the site before 2.0 (`tasks/rejected/0096-71-docs-published-from-branch.md`). After the merge the trigger stays: a later push to the old branch (a fix pushed to the wrong branch, a rebase) publishes its docs over the docs of `main`, which users of 2.0 read then.

## Fix
Remove `96-documentation` and its comment from the `push` branches of `docs.yml` in the pull request that releases 2.0, so that the merge leaves only `main`.

## Done when
- [ ] `docs.yml` publishes from `main` only

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0). Not a reversal of 0096-71: that decision covered the time before the merge.
