---
type: bug
priority: low
created: 2026-10-31
closed: 2026-11-01
---
# AdventureWorks `PostDeployment` scripts never run

## Problem
`src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/PostDeployment` holds `query-store.sql` (enables Query Store) and `documentaion.sql` (extended properties). DbLive only knows the folders `BeforeDeploy` and `AfterDeploy` (settings `BeforeDeployFolder`, `AfterDeployFolder`), and the demo's `settings.json` does not rename them. The folder is copied to the output and silently ignored: an unknown folder in `Scripts` is not an error.

Verified by reading the code (`DbLiveProject.GetFolderItemsAsync`, `DbLiveSettings.AfterDeployFolder`); not run.

## Where
- `src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/PostDeployment/`
- `src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/settings.json`

## Fix
- [x] Rename the folder to `AfterDeploy`, or set `"AfterDeployFolder": "PostDeployment"`.
- [x] AfterDeploy runs on every deployment, so both scripts must be idempotent. `documentaion.sql` calls `sp_addextendedproperty`, which fails when the property already exists: the second deployment would fail. Switch to "add if missing, else update", or move it to a migration.
- [x] Rename `documentaion.sql` → `documentation.sql`.
- [x] Should DbLive warn about unknown folders in the scripts root? It would have caught this.

## Done when
- [x] Scripts run on every deployment without errors, AdventureWorks tests green

## Notes
- Renamed the folder rather than setting `AfterDeployFolder`: the demo shows the default layout.
- Not a migration: a migration runs once, and the descriptions are easier to keep in one file next to the schema they describe. AfterDeploy runs after code, so descriptions of views or procedures can be added there later.
- `documentaion.sql` was Latin-1 (`Résumé` in two descriptions); DbLive reads scripts as UTF-8, so it would have written `R�sum�`. Converted to UTF-8.
- `query-store.sql` is idempotent as is (`ALTER DATABASE ... SET QUERY_STORE = ON`). It can't run inside a transaction, which is fine with the demo's `TransactionWrapLevel: Migration`.

## Result
- `PostDeployment/` → `AfterDeploy/`, `documentaion.sql` → `documentation.sql` (UTF-8).
- `documentation.sql` creates a temporary procedure `#SetExtendedProperty` with the arguments of `sp_addextendedproperty`: it calls `sp_updateextendedproperty` when `fn_listextendedproperty` finds the property, `sp_addextendedproperty` otherwise. The 538 calls now go through it; the procedure is dropped at the end. All batches of a script run on one connection (`ServerConnection.ExecuteNonQuery`), so the temporary procedure lives across `GO`.
- `[SqlDeployFact]` `ReleaseDeploysAndRollsBack` runs AfterDeploy three times (release, breaking, breaking again) and checks that the last run does not change the schema: green, together with the `[SqlFact]` tests (4 of 4). Its log shows both scripts deployed in each of the three runs; the first run takes the add branch, the next two the update branch, and a wrong branch would fail.
- The warning about unknown folders is not decided: moved to `tasks/ideas/warn-unknown-script-folders.md`.
- `docs/examples/index.md`: the AdventureWorks row mentions the AfterDeploy scripts. DbLive behavior did not change, so `docs/llms-full.txt` stays as is.
