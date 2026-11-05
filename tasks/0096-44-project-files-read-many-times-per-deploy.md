---
type: chore
priority: low
created: 2026-11-05
---
# One deployment reads and hashes the project files many times

## Problem
Each call of `IDbLiveProject` (`GetMigrationsAsync`, `GetCodeGroupsAsync`, `GetCodeBreakingItemsAsync`, ...) runs the checks of `GetFiles` again (paths differing only by case, files outside the project folders, breaking and test files) and reads and hashes the files again. With `SetScriptsFolder` that is a walk over the disk each time.

In one deployment, `GetMigrationsAsync` is called by the checksum validation, the saver, the downgrade (removed by 0096-46), the migrations and the breaking phase; the code is read by `CodeDeployer`, by `MarkRemovedFilesAsync` and by the breaking guard. `MarkRemovedFilesAsync` calls `GetCodeBreakingItemsAsync()`, which reads the content of every breaking script only for its path.

On SQL Server, `MarkMissingCodeAsRemovedAsync` compares with `relative_path not in (select lower(p.value(...)) from @project_paths.nodes(...))`: the plan may parse the XML again for each row of `dblive.code` and compute `lower()` for each comparison. With 5000 rows and 5000 paths that can be O(N×M) on every deployment with `DeployCode`.

Nothing is wrong in the result; it costs time on large projects. Not measured.

## Where
- `src/DbLive/Project/DbLiveProject.cs` (`GetFiles` and the methods that read files)
- `src/DbLive/Deployers/Code/CodeDeployer.cs:46` (`MarkRemovedFilesAsync`)
- `src/DbLive.MSSQL/MsSqlDA.cs:486` (`MarkMissingCodeAsRemovedAsync`)

## Fix
- [ ] Measure first: a project with 3000 code files and 500 migrations, time of a deployment where nothing changed, on SQL Server and PostgreSQL.
- [ ] Read the project once per deployment (cache the files and their hashes in the scope of one `DeployAsync`), so the checks run once too.
- [ ] `MarkRemovedFilesAsync` takes the paths without reading the content.
- [ ] SQL Server: insert the parsed paths into a table variable with a primary key once, then `not exists`.

## Done when
- [ ] Measured before and after, numbers in Notes
- [ ] Existing tests pass

## Notes
Found in the code review of 2026-11-05 (#96). The round trip per file of the breaking guard is in 0096-41.
