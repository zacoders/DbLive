---
type: chore
priority: low
created: 2026-11-17
closed: 2026-11-22
---
# Closing a task after the release publishes another release

## Release note
None.

## Problem
Every push to `main` is a release ("Releases" in Development.md): `cicd.yml` publishes the packages, tags the commit and creates a GitHub Release, and GitVersion (Mainline) gives each merge at least a patch bump. Two open tasks can be closed only after 2.0.0 is on `main`:

- `0096-documentation.md`: "Branch merged to `main`" under `## Done when`, and the check of the links from `llms.txt` to `raw.githubusercontent.com/.../main/` (D5).
- `0096-49-post-release-checks-2.0.0.md`: the checks of the published release.

The commit that moves them to `tasks/done/` is a release of its own: 2.0.1, with packages that do not differ from 2.0.0. With the notes `None.` the GitHub Release gets the title of the PR as its note. Without `## Release note` the step "Build release notes" fails and `main` is red. The same holds for any later change of the docs or the tasks alone (`docs.yml` publishes the site on its own).

Found by the second code review of 2026-11-17 (finding 10).

## Where
- `.github/workflows/cicd.yml:6-8` (`on: push: branches: [ "main" ]`, no `paths-ignore`)
- `Development.md` ("Releases": "Every merge to `main` is a release")
- `tasks/0096-documentation.md` (`## Done when`), `tasks/0096-49-post-release-checks-2.0.0.md`

## Fix
- [x] Which way? None of the three (Notes, 2026-11-22): the checks after a release are not a task.
  1. Close them in the next PR that changes code, with its release.
  2. Skip `cicd.yml` on a push that changes only `tasks/` and `docs/`: `paths-ignore` on `push` only, so the checks of a pull request still run. The tasks closed by such a push go into the notes of the next release, which reads `tasks/done/` since the previous tag.
  3. Accept a 2.0.1 without changes.

## Docs to update
- `Development.md`: "After a release" under "Releases"

## Done when
- [x] The decision of the maintainer is under `## Notes` with its date; with option 2, `cicd.yml` and "Releases" in Development.md changed

## Notes
- 2026-11-22, decision of the maintainer: the checks after a release are not a task, they are checks. A fix after a release is a task of its own and ships with its release. So nothing is closed after 2.0.0, and no release without changes is needed for it.
- Option 2 had a problem of its own, not verified: GitVersion 5 in Mainline mode raises the version for every merge into `main`, tagged or not, as far as is known. After a skipped merge `main` would get 2.0.2, while `release-notes.cs` (`VersionBump.Next`) predicts one bump from the previous tag, 2.0.1, in the PR check; a `MinCompatibleVersion` raised to the predicted version would then pass the PR check and stop the release on `main`.

## Result
The checks after a release are in "After a release" in `Development.md`, for every release: the version and the GitHub Release, the symbols packages, NuGet Package Explorer, the links from `docs/llms.txt` to `main`. `0096-49-post-release-checks-2.0.0.md` moved to `tasks/rejected/`. `0096-documentation.md` no longer waits for the merge: the link check of D5 moved to the same list, and "Branch merged to `main`" left its `## Done when`, so it closes in the last commit of the branch and its note goes into 2.0.0. `cicd.yml` is unchanged.
