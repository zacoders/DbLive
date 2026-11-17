---
type: chore
priority: low
created: 2026-11-17
---
# An unused `DeployParameters` parameter and two `Skipped` statuses that are never written

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

- [ ] A row with the status `skipped` would fail to read after the removal. No released version from 1.3.0 on writes it; is a check of the versions before 1.3.0 needed, or is that enough?
- [ ] Does the removal of a stored value that was never written touch `MinCompatibleVersion` ("Compatibility of the `dblive` tables" in Development.md)? An older DbLive reads nothing new, so it should not; to confirm.

## Done when
- [ ] `IFolderDeployer.DeployAsync` has no `DeployParameters` parameter
- [ ] The two `Skipped` members removed, the snapshot test updated and passing
- [ ] Unit tests pass
