---
type: chore
priority: high
created: 2026-11-09
---
# The docs site is published from the branch `96-documentation`

## Problem
`.github/workflows/docs.yml` publishes the site on a push to `main` and to `96-documentation`. Until the branch is merged, every push publishes zacoders.github.io/DbLive and its `llms-full.txt` with the behavior of 2.0, while nuget.org has 1.5.0: users and agents read docs that do not match the package.

The last item of D6 in `0096-documentation.md` says to remove the branch before the merge. It was found by the code review of 2026-11-05 and is still open. Found again by the code review of 2026-11-09 (finding 7).

Removing the branch does not take back what it already published: the site stays as the last push of the branch left it until the next push to `main`. The workflow has no `workflow_dispatch`.

## Where
`.github/workflows/docs.yml:7`

## Fix
Remove `- 96-documentation` from `on.push.branches`.

- [ ] Now, or in the last commit before the merge? Until the merge and the release of 2.0, the site shows 2.0 either way.

## Done when
- [ ] `96-documentation` removed from `docs.yml`
- [ ] The last item of D6 in `0096-documentation.md` ticked
