---
type: chore
priority: low
created: 2026-11-17
closed: 2026-11-22
---
# An unused `DeployParameters` parameter and two `Skipped` statuses that are never written

## Release note
None. An internal cleanup; deployments run and write the `dblive` tables as before.

## Problem
Two leftovers that make a reader think about behavior DbLive does not have:

- `IFolderDeployer.DeployAsync(project, projectFolder, parameters)` never reads `parameters`. The signature suggests that BeforeDeploy and AfterDeploy follow `DeployCode` / `DeployMigrations`. They run on every deployment, also with `DeployParameters.Breaking`.
- `MigrationItemStatus.Skipped` and `CodeItemStatus.Skipped` are never assigned: not in 2.0, and not in 1.5.0 or 1.3.0 (verified with `git grep -i skipped` on both tags: only the two enum members). Older versions were not checked.

Found by the code review of 2026-11-17 (finding 10).

## Where
- `src/DbLive/Deployers/Folder/IFolderDeployer.cs:5`, `src/DbLive/Deployers/Folder/FolderDeployer.cs:13`
- `src/DbLive/Deployers/DbLiveDeployer.cs:40` and `:48` (the two calls)
- `src/DbLive/Adapter/MigrationItemStatus.cs:6`, `src/DbLive/Adapter/CodeItemStatus.cs:6`
- `src/DbLive.Tests/SelfDeployer/DbLiveTablesSnapshotTests.cs:45` and `:50` (`skipped` in the snapshot of the stored values)

## Fix
Remove the `parameters` parameter from `IFolderDeployer.DeployAsync` and from its two calls.

Remove the two `Skipped` members and their lines from the snapshot of the `dblive` tables. The enums are internal, and the status is stored as text (`Status.ToString().ToLowerInvariant()`), so the other values stay as they are.

- [x] A row with the status `skipped` would fail to read after the removal. No released version from 1.3.0 on writes it; is a check of the versions before 1.3.0 needed, or is that enough? Checked all tags (see Notes): no DbLive version ever wrote `skipped` to the `dblive` tables. Accepted by the maintainer 2026-11-22.
- [x] Does the removal of a stored value that was never written touch `MinCompatibleVersion` ("Compatibility of the `dblive` tables" in Development.md)? An older DbLive reads nothing new, so it should not; to confirm. Confirmed: this DbLive writes no value an older one does not know, so the constant stays and only the snapshot is updated. Accepted by the maintainer 2026-11-22.

## Done when
- [x] `IFolderDeployer.DeployAsync` has no `DeployParameters` parameter
- [x] The two `Skipped` members removed, the snapshot test updated and passing
- [x] Unit tests pass

## Notes
- Verified: `git grep` of every release tag from 0.6.1 to 1.5.0 for `Status.Skipped`, `'skipped'` and `"skipped"`. Only 0.6.1–0.9.5 wrote `skipped` (`MigrationItemDeployer.MarkAsSkipped`), and they were EasyFlow: they wrote to `easyflow.migration`. 1.0.0 creates the `dblive` schema from scratch and has no reference to `easyflow`, so no row of the `dblive` tables ever had the status `skipped`. 1.0.0–1.5.0 do not write it.
- The docs never listed `skipped`: `dblive.migration.status` is `none`/`applied`, `dblive.code.status` is `applied`/`error`/`removed` (Section 15 of `docs/llms-full.txt`, `docs/deployment/index.md`). No docs change.
- No code casts the two enums to or from numbers, so the shift of the numeric values after the removal changes nothing.

## Result
`IFolderDeployer.DeployAsync(project, projectFolder)` takes no `DeployParameters`; `DbLiveDeployer` and the tests (`DbLiveDeployerTests`, `FolderDeployerTests`) call it without them. `MigrationItemStatus` is `None`/`Applied`, `CodeItemStatus` is `Error`/`Applied`/`Removed`; the snapshot of `DbLiveTablesSnapshotTests` lost the two `skipped` lines, `MinCompatibleVersion` is not raised. No behavior change, no docs. Tests: the solution builds without warnings, `DbLive.Tests` pass (549).
