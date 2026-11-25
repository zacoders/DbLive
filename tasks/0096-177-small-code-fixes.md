---
type: chore
priority: low
created: 2026-11-25
---
# Small code fixes from the review of 2026-11-24/25

## Problem
1. **The hint of a PostgreSQL error is lost.** `GetSqlError` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:1035-1048`) writes `Severity`, `SqlState`, `Message`, `Position`, `InternalQuery`, `Where`, `Routine`, as §11.2 says, but not `PostgresException.Hint`: for `42883` "No function matches the given name and argument types. You might need to add explicit type casts.", which is often the whole answer. Npgsql 10 already has `POSITION: n` and `DETAIL: ...` in `Message`, so `Position` is there twice; `Detail` reads "Detail redacted ... Specify 'Include Error Detail'" unless the connection string sets it. Verified by the review of 2026-11-25.
2. **Comments.**
   - `src/DbLive.PostgreSQL/PostgreSqlSchemaSnapshot.cs:6`: "PostgreSQL 12 or later". True of the query; DbLive supports 13 or later (§2).
   - `src/DbLive/Adapter/IDbLiveDA.cs:176`: `SaveCodeItemAsync` has `<exception cref="DbLiveSqlException"/>`; both providers throw the exception of the provider.
3. **A `ProjectId` with `ẞ` or the Kelvin sign fails every deployment after the first.** The value is stored with `ToLowerInvariant()` (`ProjectIdTools.Normalize`, `src/DbLive/Common/ProjectIdTools.cs:10`) and compared with `OrdinalIgnoreCase` (`ProjectIdTools.Equals`, used at `src/DbLive/Deployers/ProjectIdValidator.cs:97`). For a few letters the two differ (`ẞ` → `ß`, `K` (U+212A) → `k`), as §4 says of paths. `"ProjectId": "Straẞe-db"`: the first deployment logs `ProjectId bound to database: straße-db.`, the second fails with `ProjectIdMismatchException: ... Settings ProjectId: Straẞe-db, database ProjectId: straße-db.`, and so does every later deployment and `RunAllTestsAsync`. Verified by the review of 2026-11-25 on PostgreSQL 18. §14: "case-insensitive, stored lowercase". Compare `Normalize(settings)` with the stored value ordinally.
4. **A migration error that is not a `DbLiveSqlException` loses its cause.** `src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:72-75`: `Unexpected error deploying <path>. Type <type>.`, not logged, the cause only in the inner exception. §5.3 (line 303): the message "contains the SQL error, or the cause of an error that does not come from the server"; §11.2 (line 802): "So the log has it too". Reached by an error of the write of the migration state after the script ran (a broken connection after a long `None` script: the script stays, not marked `applied`), or a commit that fails other than by its timeout. `CodeItemDeployer` builds its message with `WithSqlErrorOrMessage` for any exception: do the same, and log. Read in the code.
   - The same family: the writes of the rows after a successful script are outside the `try`, so their errors come without the file: `src/DbLive/Deployers/Folder/FolderDeployer.cs:181-188` (`MarkItemAsAppliedAsync`), `src/DbLive/Deployers/Code/CodeBreakingDeployer.cs:65-74` (`SaveCodeItemAsync`; 0096-89 fixed the write of the error row, which is gone since). §7 and §6.1: the message names the file.
5. **The log of a migration under `Deployment` shows an isolation level that is not used.** `MigrationItemDeployer.cs:18, 88-95`: `Deploying <path>. ... Transaction Deployment, isolation level {isolationLevel}` prints the level of the item settings; the script runs at the level of the deployment transaction (§9.3, line 712). Print the level of the deployment transaction there. Read in the code.
6. **No warning for a `CodeSubFoldersDeploymentOrder` entry in a project without code files.** `src/DbLive/Project/DbLiveProject.cs:50-53` returns before the loop that warns (57-69). §6 (line 345): "An entry that matches no code files logs a warning and forms an empty group." Verified by the review: a project with only `Migrations/` and `["schemas"]` logs nothing. Warn there too, or say "in a project with code files".
7. **`relative_path` and `name` of `dblive.migration` keep the old values after an unchanged migration file is moved.** `src/DbLive/Deployers/Migrations/MigrationsSaver.cs:147-150` skips a row whose hash matches; the providers update `relative_path` only with a new hash. §5.2 recommends moving old migrations to `Migrations/Old/`, and the query of §15 shows `relative_path`. Diagnostic only: matching is by `(version, type)`. Update the row when the path or the name differs?
8. **SQL Server `PRINT` messages in the error of a script.** `GetSqlServerError` (`src/DbLive.MSSQL/MsSqlDA.cs:1167-1178`) lists every item of `SqlException.Errors`, and SqlClient puts the info messages of the failed batch there and into `e.Message`: `print 'progress 1'; print 'progress 2'; select 1/0;` gives `Msg 8134, Level 16, ... Divide by zero error encountered.` and then `Msg 0, Level 0, State 1, Line 1` / `progress 1` / `Msg 0, ...` / `progress 2`. Verified by the review on SQL Server 2025. §11.2 (line 800): "for each error". A migration that prints its progress and fails gets every printed line in its message.
   - [ ] List only errors (`Class > 10`), or say in §11.2 that the info messages of the failed batch are included?

## Fix
1. Add `Hint` (and `Detail`?) to the text of `GetSqlError`; drop `Position` if `Message` has it already.
   - [ ] Mention `Include Error Detail=true` in §11.2 for the detail of an error?
2. Correct the comments.
3–8. As above, each with a unit test where it changes behavior.

## Docs to update
- `docs/llms-full.txt` §6 (6: the warning), §11.2 (1, 8), §14 (3, if it says more on the comparison)

## Done when
- [ ] 1–8 fixed, with tests

## Notes
- Found by the code review of 2026-11-25.
