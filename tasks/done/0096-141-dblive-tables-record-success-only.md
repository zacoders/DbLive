---
type: chore
priority: high
created: 2026-11-23
closed: 2026-11-23
breaking: true
---
# Write only successes to the `dblive` tables; a failure is in the exception and the log

## Release note
**Failures are no longer written to the `dblive` tables.** The tables keep what the next deployment needs and the history of successful runs; an error is in the exception and the log, with every `TransactionWrapLevel`. A failed code file runs again on the next deployment, as before. `RunAllTestsAsync` writes nothing: each result is in the log. The first deployment with this version deletes the rows with `status = 'error'` from `dblive.code` and drops the columns `error` of `dblive.code` and `dblive.migration` and the table `dblive.unit_test_run`.

The message of `CodeDeploymentException` now holds the error of the server on the lines after the one that names the file, as `MigrationDeploymentException` and `FolderDeploymentException` do: on SQL Server `Msg`, `Line`, `Procedure` and the line of the file where the failed `GO` batch starts, on PostgreSQL `SqlState`, the position of the error and `Hint`. So the log has it too.

To upgrade: read failures from the log or the exception; remove queries and reports that read `dblive.code.error`, `dblive.migration.error`, `status = 'error'` or `dblive.unit_test_run`. A test that compares the whole message of a `CodeDeploymentException` gets the error of the server after its first line.

## Problem
DbLive writes the failure of an item to its tables in some cases only:

| Table | Failure written |
|---|---|
| `dblive.migration` | never: `error` is always NULL (`MigrationItemDeployer.cs:111`); a failed migration throws and leaves its row `none` |
| `dblive.folder_items` | never: no error column |
| `dblive.code`, code file | `status = 'error'` and `error`, only outside the deployment transaction (`CodeItemDeployer.SaveErrorRowAsync`) |
| `dblive.code`, breaking script of the Code folder | the same (`CodeBreakingDeployer.SaveErrorRowAsync`) |
| `dblive.unit_test_run` | always: `pass = 0` and `error` (`UnitTestsRunner`) |

- A user cannot rely on it: whether a table has the error depends on the kind of item and on `TransactionWrapLevel`. The exception and the log have it in every case.
- DbLive reads none of it: `CodeRows.IsDeployed` looks only at `applied` rows, and `dblive.unit_test_run` is written and never read.
- The writes caused bugs of their own: 0096-51 (the write in an aborted PostgreSQL transaction hid the SQL error), 0096-60 (`nvarchar(4000)`, `MsSqlDA.FitErrorColumn`), 0096-89 (an unguarded write in `catch` replaced the error).
- 2.0 is not released (the last tag is `1.5.0`), and `DbLiveCompatibility.MinCompatibleVersion` is 2.0.0 already: the columns and the table can be dropped now without raising it. After the release a drop raises `MinCompatibleVersion` and blocks a rollback to 2.0.

The tables keep what the next deployment needs (what is applied) and the history of successful runs: the rows of the breaking scripts of the Code folder and `dblive.folder_items`.

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs` and `CodeBreakingDeployer.cs` (`SaveErrorRowAsync`), `CodeDeployer.cs:135` and `:219` (the log of a failed file)
- `src/DbLive/Deployers/Testing/UnitTestsRunner.cs` (`SaveUnitTestResultAsync`); XML docs of `src/DbLive/Testing/IDbLiveTester.cs`, `DbLiveTester.cs`, `src/DbLive/Deployers/Testing/UnitTestsFailedException.cs`
- `src/DbLive/Adapter/CodeItemStatus.cs` (`Error`), the `ErrorMessage` of `CodeItemDto` and `MigrationItemStateDto`, `UnitTestItemDto`, `IDbLiveDA`
- `src/DbLive.MSSQL/MsSqlDA.cs` (`FitErrorColumn`, the writes of `error`, `SaveUnitTestResultAsync`), `src/DbLive.PostgreSQL/PostgreSqlDA.cs`
- self-migrations: `src/DbLive.MSSQL/SQL/`, `src/DbLive.PostgreSQL/SQL/`
- tests: `ErrorColumnTests`, `MsSqlDAFitErrorColumnTests`, `CodeBreakingTests`, `CodeFileTransactionTests`, `ScriptEndsTransactionTests`, `SqlTestResultSetTests`, `UnitTestsRunnerTests`, `CodeItemDeployerTests`, `CodeBreakingDeployerTests` and others (about 20 files read or assert the error rows)

## Fix
1. First: the SQL error of a code file and of a breaking script of the Code folder goes to the log and to the message of `CodeDeploymentException`, as it does for a migration (`MigrationItemDeployer`) and a BeforeDeploy/AfterDeploy file (`FolderDeployer`). Today `DbLiveSqlException.SqlError` of a code file (on SQL Server `The batch starts at line <N> of the file.`, `Msg ..., Line ...`, `Procedure: ...`) is only in `dblive.code.error` and in the property: the log and the message have the message of the server without the lines. Without this step the drop loses it.
2. A code file and a breaking script of the Code folder write no row on failure: remove both `SaveErrorRowAsync`. The row of a code file stays the last success.
3. `RunAllTestsAsync` writes nothing; `dblive.unit_test_run` is dropped. Writing only passes would leave `pass = 1` of an earlier run after a failure.
4. A self-migration on both providers: drop `error` of `dblive.code` and `dblive.migration`, delete the rows with `status = 'error'` (for the next deployment the same as now: the file is not applied and runs), drop `dblive.unit_test_run`. Remove `CodeItemStatus.Error`, `FitErrorColumn` and the error fields of the DTOs. `MinCompatibleVersion` stays 2.0.0.

- [x] Does a failed code file have to withdraw the earlier success of its row (delete the row), for a file that failed in part? Decided by the maintainer on 2026-11-23: no. A failed code file writes nothing; the case of an In-Memory OLTP file applied in part gets one sentence in §6 (Docs to update). Analysis in Notes.

## Docs to update
Line numbers as of 2026-11-23.
- `docs/llms-full.txt` §6, the paragraph on In-Memory OLTP (line 341), after "keep such an object alone in its file.": "Its row in `dblive.code` keeps the last success: when such a file fails in part and its previous content comes back (`git revert`, a redeploy of the previous release), it is skipped as `[hash match]`; change the file or delete its row to run it again." The same in `docs/deployment/code.md`, where it describes In-Memory OLTP files.
- `docs/llms-full.txt`: §6.1 (the row of a breaking script, line 390), §8.2 (line 515), §11.4 (line 810), §15 (the tables, lines 1011-1016, and the queries, 1032-1034; the paragraph on the format of `error` moves to the description of the exception), §17 (`CodeDeploymentException`, `UnitTestsFailedException`, line 1082)
- `docs/deployment/code.md` (line 101), `docs/deployment/breaking-changes.md` (line 56), `docs/deployment/index.md` (line 192), `docs/unit-testing/index.md` (line 351)
- Release notes of the tasks closed since 1.5.0 that name `dblive.code.error`, `status = 'error'` or `dblive.unit_test_run`: 0096-60, 0096-68, 0096-85, 0096-89, possibly 0096-17. The same search found 0096-15, 0096-18 and 0096-44: check them.

## Done when
- [x] The SQL error of a code file and of a breaking script of the Code folder is in the log and in the exception message, with tests on both providers
- [x] No failure is written to the `dblive` tables; the `error` columns and `dblive.unit_test_run` are dropped by a self-migration on both providers, with tests
- [x] Release notes of the earlier tasks of 2.0 corrected
- [x] Docs updated
- [x] 0096-131 closed or reduced to `dblive.code.created_utc`

## Notes
- 2026-11-23, the maintainer: a failure is an error in the log, and the user works with the log; going to the tables to see an error is not needed. Write only successes, for example so that the next deployment does not run the item again.
- Related tasks. 0096-131: its question on `dblive.migration.error` is answered by the drop, `dblive.unit_test_run.content_hash` goes away, `dblive.code.created_utc` stays to document. 0096-133: the key of `dblive.unit_test_run` goes away. 0096-112: `dblive.unit_test_run.error` goes away, the setup marker is needed in the log and in `ErrorMessage` only.
- Withdrawing the earlier success (decided on 2026-11-23: no, see Fix). Only a code file has a row that decides whether the next deployment runs it (`[hash match]`): an applied migration cannot change (checksum validation) and a failed one stays `none`; BeforeDeploy/AfterDeploy files and the breaking scripts of the Code folder run on every deployment.
  - After a failure the code file runs again on the next deployment whenever its content is the failed one or a new one: the hash differs from the applied row. It is skipped only when the content returns to the hash of the last success (`git revert`, a redeploy of the previous release), and that is wrong only when the failed run changed the database in part. Today the error row overwrites the applied row, so the file runs again then.
  - Outside the deployment transaction a code file runs in a transaction of its own and is rolled back as a whole on both providers; in the deployment transaction a failed deployment rolls back everything. The database keeps the last success, and the applied row is right.
  - In part only: (a) SQL Server, a code file with In-Memory OLTP DDL (`RunsCodeItemWithoutTransaction`): its `GO` batches commit one by one, for example the `drop` succeeds and the `create` fails. §6 advises one object per file; with `create or alter` it is one batch, and nothing is applied in part. (b) A file that ends its transaction with `COMMIT`: it fails on every run, so the deployment test catches it before production. (c) The connection breaks between the commit of the file and the write of its row: the file is applied, the row is old. The error row of today does not reliably help here: its write goes to the same server right after the failure.
  - So it takes a multi-batch In-Memory OLTP file, a failure in production after an earlier batch committed, and the return of the previous content before any later successful deployment. The object is then missing or old, which shows at once, and §6 gives the fix: change the file or delete its row.
- Self-migrations 08 and 09 of SQL Server and 05 and 06 of PostgreSQL deduplicated and renamed the paths of `dblive.unit_test_run`; that part is removed. Tests simulate an older database by lowering `dblive.version` on a database that already ran the new self-migration (13 / 10), and 08/09 (05/06) then failed on the missing table. They are not released (1.5.0 has SQL Server 01-06 and PostgreSQL 01-03), a database that ran them never runs them again, and a 1.5 database loses the table in 13 / 10 anyway. `DbLiveTablesSnapshotTests` shows the changed hashes.
- The message of `CodeDeploymentException`: the first line stays as it was (`Deploy code file error. File path: <path>`, with the reason it is not retried), and the error follows on the next lines, `ExceptionExtensions.WithSqlErrorOrMessage`: `SqlError`, or the message of the exception for an error that does not come from the server, as in `MigrationItemDeployer` and `FolderDeployer`. Lines are joined with `Environment.NewLine`, not with a raw string literal: the newline of a literal is the one of the source file, and the files of the repository mix LF and CRLF. A code file's error reaches the log through the exception: `CodeDeployer` logs the `CodeDeploymentException`, and its message now has the error. The breaking script logs its `CodeDeploymentException` too, instead of the inner exception.
- `RunAllTestsAsync` still requires the DbLive tables and checks the minimum DbLive version, though it no longer writes the tables: the check of the ProjectId reads `dblive.version`, and a database without the tables was never deployed. Unchanged behavior; the comments in `DbLiveTester` now give these reasons.
- `TestRunResult.StartedUtc`, `CompletedUtc` and `ExecutionTimeMs` (internal) are read by nothing in production now; left as they are, `UnitTestItemRunnerTests` covers them.

## Result
- **Code.** `CodeItemDeployer` and `CodeBreakingDeployer` write no row on failure (both `SaveErrorRowAsync` removed); the message of their `CodeDeploymentException` has the error of the server after its first line (`ExceptionExtensions.WithSqlErrorOrMessage`). `UnitTestsRunner` writes nothing and no longer takes `IDbLiveDA`. Removed: `IDbLiveDA.SaveUnitTestResultAsync`, `UnitTestItemDto`, `CodeItemStatus.Error`, `ErrorMessage` of `CodeItemDto` and `MigrationItemStateDto`, `MsSqlDA.FitErrorColumn` with `ErrorColumnLength` and `TruncatedMark`, the `error` column in the queries of both providers.
- **Self-migrations.** `13.record-success-only.sql` (SQL Server) and `10.record-success-only.sql` (PostgreSQL), re-runnable: delete the rows with `status = 'error'`, drop `error` of `dblive.code` and `dblive.migration`, drop `dblive.unit_test_run`. The parts on `dblive.unit_test_run` are removed from the unreleased 08/09 and 05/06 (Notes). `MinCompatibleVersion` stays 2.0.0; snapshot updated.
- **Tests.** New `CodeFileFailureTests` on both providers (replace `ErrorColumnTests`; `MsSqlDAFitErrorColumnTests` deleted): the message of a failed code file has the error of the server (SQL Server: the exact text with the batch line and `Procedure`, a 7000-character error whole; PostgreSQL: `PostgreSQL error:`, `SqlState`, `Position`) and no row is written; a failed file keeps the row of its last success and runs again on the next deployment; the self-migration removes the error rows, the columns and the table of an older database. `CodeBreakingTests` on both providers check the error of the server in the message and that no row is written, with `Migration` and `Deployment`. Unit tests of the deployers and of `UnitTestsRunner` check the message, the log and that nothing is written. Tests that read the error rows or `dblive.unit_test_run` now check the message or run the tests one by one. Verified: `DbLive.Tests` 549, `DbLive.MSSQL.Tests` 153, `DbLive.PostgreSQL.Tests` 86, all passed.
- **Docs.** `docs/llms-full.txt` §6, §6.1, §8.2, §11.2 (the format of `SqlError` moved here from §15), §11.4, §15 (tables, conversion of older databases, queries), §17; `docs/deployment/code.md`, `breaking-changes.md`, `index.md`, `docs/unit-testing/index.md`.
- **Other tasks.** Release notes of 0096-17, 0096-60, 0096-68, 0096-85 corrected; 0096-89 is `None.` now (the write it guarded is gone, and breaking scripts of the Code folder are new in this release). 0096-15, 0096-18 and 0096-44 checked: their notes do not name the removed columns. 0096-131 closed: only `dblive.code.created_utc` was left, now in §15. Notes added to 0096-112 and 0096-133.
