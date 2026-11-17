---
type: chore
priority: low
created: 2026-11-05
closed: 2026-11-07
---
# One deployment reads and hashes the project files many times

## Release note
A deployment where nothing changed makes fewer round trips to the database: the code deployment and the guard of the breaking phase read `dblive.code` with one query instead of one per code file, and the `verified_utc` of the skipped files is set with one command. A builder now reads the project files once, on first use, like `settings.json`: a later deployment from the same builder does not see files changed since, so create a new builder for each build you deploy (a release pipeline does).

## Problem
Each call of `IDbLiveProject` (`GetMigrationsAsync`, `GetCodeGroupsAsync`, `GetCodeBreakingItemsAsync`, ...) runs the checks of `GetFiles` again (paths differing only by case, files outside the project folders, breaking and test files) and reads and hashes the files again. With `SetScriptsFolder` that is a walk over the disk each time.

In one deployment, `GetMigrationsAsync` is called by the applied versions check (`AppliedMigrationsValidator`), the checksum validation, the saver, the migrations and the breaking phase; the code is read by `CodeDeployer` and by the breaking guard, the breaking scripts of the Code folder by `MarkRemovedFilesAsync` and by `CodeBreakingDeployer`. About 11 walks over the folder per deployment.

`CodeItemDeployer` made two round trips per code file on every deployment, also when nothing changed: `FindCodeItemAsync`, then `MarkCodeAsVerifiedAsync` for a `[hash match]`. 3000 code files made 6000 round trips, on 10 threads by default, on one thread with `TransactionWrapLevel.Deployment`.

On SQL Server, `MarkMissingCodeAsRemovedAsync` compares with `relative_path not in (select lower(p.value(...)) from @project_paths.nodes(...))`: the plan may parse the XML again for each row of `dblive.code` and compute `lower()` for each comparison. With 5000 rows and 5000 paths that can be O(N×M) on every deployment with `DeployCode`. Not verified by a plan.

The breaking guard (`BreakingChangesDeployer.ValidateCodeDeployedAsync`) runs one `FindCodeItemAsync` per code file, one after another: 3000 code files make 3000 round trips on every breaking deployment. Its "is deployed" check (`row != null && Status == Applied && MatchesHash`) is a copy of the one in `CodeItemDeployer`.

Nothing is wrong in the result; it costs time on large projects. Not measured.

## Where
- `src/DbLive/Project/DbLiveProject.cs` (`GetFiles` and the methods that read files)
- `src/DbLive/Deployers/Code/CodeDeployer.cs`, `src/DbLive/Deployers/Code/CodeItemDeployer.cs`
- `src/DbLive.MSSQL/MsSqlDA.cs` (`MarkMissingCodeAsRemovedAsync`)
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs` (`ValidateCodeDeployedAsync`)

## Fix
- [x] ~~Measure first~~: dropped by the maintainer, see Notes.
- [x] Read the project once ~~per deployment~~ per builder, so the checks run once too.
- [x] ~~`MarkRemovedFilesAsync` takes the paths without reading the content.~~ Covered by the cache, see Notes.
- [x] SQL Server: the paths in a table with a primary key, then `not exists`: a table-valued parameter of the new type `dblive.tvp_unique_string_list`.
- [x] The breaking guard reads all rows of `dblive.code` with one query and compares in memory; one "is deployed" check for the guard and the code deployment.
- [x] The code deployment reads all rows of `dblive.code` with one query and sets `verified_utc` of the skipped files with one command.

## Docs to update
- `docs/llms-full.txt` §3.4 (`SetScriptsFolder`: a builder reads the project once), §11.1 (`IScriptSource`: `ReadAsync` once per file)
- `docs/project/index.md` (the paragraph after the `SetScriptsFolder` example)
- `docs/llms-full.txt` §15 and `docs/deployment/index.md` ("Bookkeeping tables"): the table type `dblive.tvp_unique_string_list`

## Done when
- [x] ~~Measured before and after, numbers in Notes~~: dropped by the maintainer, see Notes.
- [x] Existing tests pass

## Notes
Found in the code review of 2026-11-05 (#96). The round trip per file of the breaking guard and its copied check moved here from 0096-41 (rejected 2026-11-07).

- 2026-11-07, the maintainer: no measurement. On a local NVMe disk reading the files costs next to nothing, so a local measurement of that part says nothing; the fix is simple enough to do without one.
- The two round trips per file of `CodeItemDeployer` were not in the first version of this task; found when checking it on 2026-11-07. They are likely the largest cost of a deployment where nothing changed: more than the reads of the files, which are cached by the OS and hashed with XxHash64.
- First try: a snapshot for one deployment (`IDbLiveProject.BeginSnapshot()`, async-local, opened by `DbLiveDeployer`), because `FolderScriptSource` enumerated the folder on every call on purpose: integration tests changed the files of one folder between deployments with the same builder (0096-31-01), and the docs promised it. 2026-11-07, the maintainer: in real life a builder deploys one build, the files do not change under it; the tests were wrong, and the snapshot solved a problem they created. So the project is read once per builder, like `settings.json` (`SettingsAccessor`) and the tests of `DbLiveTester`, and the tests create a new builder for each new build. This reverses the decision of 0096-31-01. Not breaking: `SetScriptsFolder` and the promise are new in this release; 1.5.0 had only `SetProject(assembly)`.
- `DbLiveProject` caches the list of files (with its checks) and each `FileData` (content and hash); the methods build their items from them again, which costs next to nothing. One rule for every method, tests and BeforeDeploy/AfterDeploy included. A failed check is not cached: the next read checks again and fails again.
- `MarkRemovedFilesAsync` still asks for the breaking items: with the cache the breaking scripts are read once per builder, and only for their paths when the breaking phase does not run. A separate method for the paths alone is not worth it.
- The `[hash match]` lines are now logged for all skipped files before the first file is executed, and `verified_utc` of the skipped files is set before the changed files run: the check is a fact at the time of the read.
- SQL Server, first try: the xml of the paths parsed once into a table variable with a primary key. A key on `nvarchar(4000)`, like the one of `dblive.code`, gives the warning "The maximum key length for a clustered index is 900 bytes" on every call; a clustered key holds at most 900 bytes, so `dblive.code` never stores a path longer than 450 characters, and the column became `nvarchar(450)`. 2026-11-07, the maintainer: no xml and no temporary table; a table-valued parameter of a type `dblive.tvp_unique_string_list ( value nvarchar(450) primary key )`. Self-migration 13 creates it. `MinCompatibleVersion` stays: an older DbLive does not use the type, like a new table. A longer path of a code file fails, as before, when its row is saved. The tables of the `dblive` schema keep their `nvarchar(4000)` keys: they warn once, when they are created.
- The TVP is filled from a `DataTable`: an empty list (a project without code files) is an empty table, which `IEnumerable<SqlDataRecord>` does not allow. `lower()` stays in the query, like in every other one. The paths are unique: `GetFiles` stops on paths that differ only by case, and code files and breaking scripts do not overlap.
- `FindCodeItemAsync` stays on `IDbLiveDA`: the integration tests use it. `MarkCodeAsVerifiedAsync` takes a collection of paths. Neither is a listed member of `IDbLiveDA` (docs/versioning.md), so the change is not breaking.

## Result
- `DbLiveProject` reads the project once per builder: the files are listed and checked once, and each file is read and hashed once (`_files`, `_fileData`). One deployment walks the folder once instead of about 11 times; the next deployment from the same builder reads nothing.
- `IDbLiveDA.GetCodeItemsAsync()` (all rows of `dblive.code`, one query) and `MarkCodeAsVerifiedAsync(paths, verifiedUtc)` (one command), on SQL Server and PostgreSQL.
- `CodeRows.IsDeployed(FileData)`: the one "is deployed" check. `CodeDeployer` uses it to skip deployed files before the workers start; `CodeItemDeployer` only executes and saves. The breaking guard uses it with one query instead of one per file.
- SQL Server: `MarkMissingCodeAsRemovedAsync` and `MarkCodeAsVerifiedAsync` take the paths in a table-valued parameter of the type `dblive.tvp_unique_string_list` (self-migration `13.tvp-unique-string-list.sql`), then `not exists` / `join`; no xml.
- Tests: the integration tests that deploy several builds from one folder create a new builder after each change of the files. `SetScriptsFolder_ReadsTheProjectOncePerBuilder` replaces the test that expected the folder to be read again. The hash-match cases moved from `CodeItemDeployerTests` to `CodeDeployerTests` (skip and one verify command, CRLF legacy hash, removed and failed rows run again); the guard tests read the rows with one call. Unit tests, SQL Server and PostgreSQL integration tests pass.
