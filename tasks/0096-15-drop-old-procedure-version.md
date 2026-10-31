---
type: feature
priority: medium
created: 2026-10-31
---
# Where to drop an old procedure version when the release has no migration

## Problem
When the contract of an interface procedure changes, its file is changed in place and the object is renamed (`MyStoredProc_v1` → `MyStoredProc_v2`). The old version stays in the database for old applications and is dropped in the breaking phase, after they are gone. Often such a release changes only code: no tables change, so there is no migration. There is no natural place for `drop proc dbo.MyStoredProc_v1`.

Current behavior (verified by reading the code):
- A breaking script belongs to a migration version (`<version>.breaking.sql`). A version that has a breaking script but no migration file fails when the project is read: `MigrationItemMustExistsException` (`DbLiveProject.GetMigrationsAsync`).
- A breaking script runs only with `DeployBreaking = true` and only after the migration of its version was applied (`BreakingChangesDeployer`). This is what guarantees that the drop never runs before the deployment that shipped `_v2`.
- `BeforeDeploy` and `AfterDeploy` run on every deployment, whatever the `DeployParameters` (`DbLiveDeployer` calls `FolderDeployer` unconditionally, and `FolderDeployer` does not look at the parameters). A drop in `AfterDeploy` would run with `DeployParameters.Default`, before the new application is deployed, and break the old one. Not usable for this.
- DbLive has no `PostDeployment` folder; the folder is `AfterDeploy` (setting `AfterDeployFolder`). The `PostDeployment` folder of the AdventureWorks demo is never run: task 0096-16.

Today's workaround (`docs/llms-full.txt` §18.6, `docs/deployment/breaking-changes.md`): add a new version with a trivial migration (`select 1;`) and put the drop into its breaking script. It works, but the migration exists only to carry the breaking script, and the migration list fills with empty versions.

## Where
- `src/DbLive/Project/DbLiveProject.cs` (`GetMigrationsAsync`, `MigrationItemMustExistsException`)
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs`
- `src/DbLive/Deployers/DbLiveDeployer.cs`, `src/DbLive/Deployers/Folder/FolderDeployer.cs`

## Options
- [ ] A. Keep the workaround and document it as the way. Check whether a comment-only migration file works on SQL Server and PostgreSQL, or `select 1;` is required.
- [ ] B. Allow a version with a breaking script and no migration: during the normal deployment DbLive records an empty migration of that version as applied, so "breaking runs after its version was deployed" still holds.
- [ ] C. A breaking script next to the code file (`my-stored-proc.breaking.sql` next to `my-stored-proc.sql`), run in the breaking phase once its content changes, and only after the current content of the code file was deployed. How does it interact with downgrade and with `[SqlDeployFact]` scenarios?
- [ ] D. A folder that runs only in the breaking phase (like `AfterDeploy`, but only when `DeployBreaking = true`). Scripts must be idempotent (`drop ... if exists`). How does it guarantee that the deployment which shipped `_v2` already happened? When are its scripts removed?
- [ ] Which option? Decide with the maintainer.

## Docs to update
- `docs/llms-full.txt` §5.6, §7, §18.6, §0 rule 14
- `docs/deployment/breaking-changes.md` ("Writing a breaking script"), `docs/deployment/code.md` ("Code is never dropped"), `docs/deployment/zero-downtime.md` (step 6), `docs/database-as-service.md` ("A procedure is a module with a history")

## Done when
- [ ] Decision taken
- [ ] Implemented with unit tests, or the workaround verified on both engines
- [ ] Docs updated
