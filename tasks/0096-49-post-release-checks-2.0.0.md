---
type: chore
priority: high
created: 2026-11-07
---
# Checks after the release of 2.0.0

## Problem
Some results of #96 can be checked only on the release itself, which runs on the merge of PR #98 into `main`. The tasks that made these changes are closed and in `tasks/done/`, so their release notes go into the notes of 2.0.0; their checks after the release are collected here (decision of the maintainer, 2026-11-07, in 0096-45).

## Done when
- [ ] #96 is released as 2.0.0: PR #98 merged with a title that starts with `BREAKING CHANGE:` (from 0096-19)
- [ ] The release has a GitHub Release with the notes built from the tasks (from 0096-19)
- [ ] The release on nuget.org has a symbols package (`.snupkg`) for each package (from 0096-18)
- [ ] NuGet Package Explorer shows Source Link, deterministic build and compiler flags as valid for the released packages (from 0096-18)

## Notes
- 0096-18: `dotnet-validate`, the command-line validator of NuGet Package Explorer, was not allowed in the agent session, so the check was not run on a local package either.
