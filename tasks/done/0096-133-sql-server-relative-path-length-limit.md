---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# SQL Server: a relative path longer than about 450 characters fails after its file ran

## Release note
SQL Server: a code file or a breaking script of the Code folder whose path relative to the scripts root is longer than 450 characters, or a BeforeDeploy or AfterDeploy script whose path is longer than 444, now stops the deployment when the project is loaded, before anything runs, with `ScriptPathTooLongException`, which names the file. Before, the file ran and then failed to be recorded with SQL Server error 1946, in every round of the code deployment. Such a project never deployed, so nothing that deployed before fails now. Shorten the names of the folders or of the file.

## Problem
`relative_path nvarchar(4000)` is the clustered primary key of `dblive.code` (`src/DbLive.MSSQL/SQL/02.code.sql`) and, after `folder_type`, of `dblive.folder_items` (`04.folder-items.sql`). A clustered key holds at most 900 bytes, so a path longer than 450 characters (444 in `folder_items`) cannot be stored. The comment of `PathsTableSql` in `src/DbLive.MSSQL/MsSqlDA.cs` acknowledged the limit.

A longer path makes `SaveCodeItemAsync` fail with Msg 1946 after the file has run; the file counts as failed, is retried in every round and ends in `CodeDeploymentAggregateException`. The message names the file and has the error of the server (`The index entry of length ... for the index 'pk_dblive_code' exceeds the maximum length of 900 bytes`), so it reads as if the file itself failed. A BeforeDeploy or AfterDeploy file fails the same way in `MarkItemAsAppliedAsync`. PostgreSQL has no such fixed limit. Rare: on Windows the full path of such a file is beyond 260 characters, but long paths can be enabled there, and Linux has no such limit.

## Fix
- [x] Document the limit in §16, or fail the project load on SQL Server for a longer path, with the path in the message? The maintainer, 2026-11-23: add the check; Windows with long paths enabled can go beyond the limit.
- [x] `IDbLiveDA.MaxCodePathLength` and `MaxFolderItemPathLength`: 450 and 444 on SQL Server, null on PostgreSQL.
- [x] `ProjectLoader` checks the paths of code files, breaking scripts of the Code folder and BeforeDeploy/AfterDeploy files and throws `ScriptPathTooLongException`.

## Docs to update
- `docs/llms-full.txt` §4 (path length), §12 step 1, §15 (`relative_path`), §16 (row "Length of a file path"), §17 (`ScriptPathTooLongException`)
- `docs/project/structure.md` (the paragraph after the case check)

## Done when
- [x] The check added, with unit tests and a SQL Server test of the exact limits
- [x] Docs updated

## Notes
- 2026-11-23: 0096-141 dropped `dblive.unit_test_run`; the limit stays for `dblive.code` and `dblive.folder_items`.
- 2026-11-23: two limits, not one: 444 for all files would reject code files of 445 to 450 characters, which deploy today, and make the change breaking. `folder_type` is `varchar`, the name of `ProjectFolder`: `BeforeDeploy` takes 12 bytes, so (900 - 12) / 2 = 444; `AfterDeploy` (11 bytes) gives 444 as well.
- 2026-11-23, verified (`RelativePathLengthTests` of `DbLive.MSSQL.Tests`): files with paths of 450 (code file, breaking script) and 444 characters (BeforeDeploy, AfterDeploy) deploy, also a second time (the code file is marked verified through `#paths nvarchar(450)`); an insert of 451 characters into `dblive.code` and of 445 with `BeforeDeploy` into `dblive.folder_items` fails with Msg 1946. The key length is the bytes of the data, without overhead.
- The length is counted in UTF-16 characters of the path, as `nvarchar` stores it: two bytes each, also each half of a surrogate pair. `ToStoredPath` (`ToLowerInvariant`) keeps the length.
- Not checked: migrations (`dblive.migration.relative_path` is `nvarchar(4000)`, not a key) and tests (they write nothing). PostgreSQL: a b-tree entry holds about 2704 bytes after compression, so the longest path depends on its characters and on how it compresses; no length is checked (from the PostgreSQL docs, not verified). `RelativePathLengthTests` of `DbLive.PostgreSQL.Tests` deploys paths of 1000 characters.

## Result
- `IDbLiveDA.MaxCodePathLength` / `MaxFolderItemPathLength`: `MsSqlDA` derives them from the 900 bytes of a clustered key (450, and 444 from the longest `ProjectFolder` name); `PostgreSqlDA` returns null.
- `ProjectLoader.LoadAsync` checks the paths after it reads the project, before the deployment touches the server, and throws the new public `ScriptPathTooLongException` (`RelativePath`) for the first path that is too long.
- Tests: `ProjectLoaderTests` (paths at the limits load, one character more fails for each kind of file, no check without a limit, migrations and tests not checked); `DbLive.MSSQL.Tests`: `RelativePathLengthTests` (the exact limits on a real server), `InvalidProjectTests` (a path of 451 characters creates no database); `DbLive.PostgreSQL.Tests`: `RelativePathLengthTests` (paths of 1000 characters deploy). `ScriptPaths.OfLength` in `DbLive.Tests.Common` builds a path of a given length in subfolders of at most 101 characters.
- Not breaking: every path that deployed before still deploys.
