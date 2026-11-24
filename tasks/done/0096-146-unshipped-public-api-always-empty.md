---
type: chore
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# `PublicAPI.Unshipped.txt` is always empty, checked by a unit test

## Release note
None. The workflow of the repository; the packages do not change.

## Problem
Development.md described `PublicAPI.Unshipped.txt` as a draft of the branch, moved to `PublicAPI.Shipped.txt` by `public-api.cs` before the merge. The workflow is another one: the public API goes to `PublicAPI.Shipped.txt` in the commit that adds it. Nothing checked it before the "Release notes" check of the pull request, so 0096-144 left its three lines of `FilesInSeveralProjectFoldersException` in `PublicAPI.Unshipped.txt`.

## Fix
A unit test fails while a `PublicAPI.Unshipped.txt` has lines, with the reasons in its remarks; Development.md and the comments of the scripts describe the workflow.

## Docs to update
- `Development.md` ("The public API")

## Done when
- [x] Unit test, verified to fail with a line in `PublicAPI.Unshipped.txt`
- [x] Development.md and the script comments updated

## Notes
- Maintainer, 2026-11-24: `*.Unshipped.txt` must be empty; the public API goes straight to `PublicAPI.Shipped.txt`. A unit test checks it and says in detail why.

## Result
- `PublicApiFilesTests.Unshipped_files_are_empty` (`src/DbLive.Tests`): reads every `PublicAPI.Unshipped.txt` of the packages (the folders of `src/` with `PublicAPI.Shipped.txt`, found through the `SrcDir` assembly metadata that `InternalScriptsTests` uses) and fails with the lines it finds and the command that moves them. Its remarks give the reasons: every merge is a release, so there is no unshipped API; the API change belongs to the commit of its task; a removed symbol is a deleted line of `PublicAPI.Shipped.txt`; the test fails at `dotnet test`, not at the pull request after many tasks. Verified: passes with empty files, fails when a line of `FilesInSeveralProjectFoldersException` is moved back to `PublicAPI.Unshipped.txt`.
- `Development.md` ("The public API"): `PublicAPI.Shipped.txt` is the API of the branch, `PublicAPI.Unshipped.txt` always empty; after the code fix of RS0016 run `public-api.cs` at once, not before the merge. Comments of `public-api.cs` and `release-notes/PublicApi.cs` say the same.
- The lines of 0096-144 moved to `src/DbLive/PublicAPI.Shipped.txt`.
