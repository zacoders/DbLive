---
type: bug
priority: medium
created: 2026-11-08
closed: 2026-11-08
---
# A long error text can fail the write of the error row on SQL Server

## Release note
On SQL Server, a code file, a breaking script of the Code folder or a SQL test whose error was longer than 4000 characters (for example a view with many invalid columns, or a test that compares wide values) failed the deployment or `RunAllTestsAsync` with `String or binary data would be truncated ... column 'error'` instead of its own error. The column `error` of `dblive.code` now holds the error of the server (on SQL Server `Msg`, `Level`, `State`, `Line` and the message) instead of the whole exception with its stack traces, which stay in the exception and the log. On SQL Server an error text longer than 4000 characters is cut and ends with `... [truncated]`. Nothing to change on upgrade.

## Problem
On SQL Server the column `error` of `dblive.code`, `dblive.unit_test_run` and `dblive.migration` is `nvarchar(4000)` (PostgreSQL: `text`). DbLive writes there:

- `ex.ToString()` of a failed code file (`CodeItemDeployer`) and of a failed breaking script of the Code folder (`CodeBreakingDeployer`), outside a transaction: the message and the stack traces of `DbLiveSqlException`, SMO and SqlClient.
- the error message of a failed test (`UnitTestsRunner`), which for a comparison of result sets lists the first row that differs, expected and actual.

`dblive.migration` is not affected: DbLive writes `null` to its `error` (a failed migration throws).

A text over 4000 characters makes the insert fail with "String or binary data would be truncated ... column 'error'", and that exception leaves the deployment (or `RunAllTestsAsync`) instead of the original error: no file name and no SQL error. For a test, `RunAllTestsAsync` fails with `SqlException` instead of `UnitTestsFailedException`. Verified, see Notes.

Found by the code review of 2026-11-08 (finding 3).

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:71`
- `src/DbLive/Deployers/Code/CodeBreakingDeployer.cs:59`
- `src/DbLive/Deployers/Testing/UnitTestsRunner.cs:49`
- `src/DbLive.MSSQL/SQL/01.migrations.sql`, `02.code.sql`, `03.unit-tests.sql` (the columns)

## Fix
- [x] Measure first: the length of `ex.ToString()` of a failed code file on SQL Server, and of the message of a failed comparison.
- [x] Which fix: a self-migration that makes the three columns `nvarchar(max)`; cutting the text to 4000 characters in `MsSqlDA`; or a shorter text (the message and the SQL error, without stack traces)? Decided by the maintainer on 2026-11-08: the shorter text and the cut in `MsSqlDA`, no self-migration.

## Docs to update
- `docs/llms-full.txt` §15: what `error` holds, the cut on SQL Server
- `docs/deployment/code.md` ("Things to know", Status)
- `docs/unit-testing/index.md` (the results in `dblive.unit_test_run`)

## Done when
- [x] Reproduced, or closed as not reproducible with the measured lengths
- [x] Fixed, with an integration test on SQL Server: a code file and a test whose error is longer than 4000 characters
- [x] Docs updated

## Notes
- Measured on 2026-11-08 (verified), SQL Server 2025 in a container, through `DeployAsync` and `RunAllTestsAsync` of a Release build, the text taken before the write of the row:

  | Case | Length | Outcome |
  |---|---|---|
  | Code file, one error (`Invalid object name`) | 2796 | `CodeDeploymentAggregateException` |
  | Code file, syntax error | 2776 | `CodeDeploymentAggregateException` |
  | Code file, a view with 20 invalid columns | 4161 | `SqlException`: truncated, `dblive.code`, column `error` |
  | Code file, dependency order fixed by a retry | 2732 | success |
  | Breaking script of the Code folder | 2537 | `CodeDeploymentException` |
  | Test, SQL error | 40 | `UnitTestsFailedException` |
  | Test, narrow row differs | 136 | `UnitTestsFailedException` |
  | Test, 20 SQL errors | 829 | `UnitTestsFailedException` |
  | Test, values of 2100 characters on each side | 4327 | `SqlException`: truncated, `dblive.unit_test_run`, column `error` |

- `ex.ToString()` of a code file was about 2700 characters of stack traces (SqlClient, SMO, DbLive) for any error, and held the SQL message twice. It did not hold the line of the error: `DbLiveSqlException.SqlError` (`Msg`, `Level`, `State`, `Line`) is a property, and `Exception.ToString()` does not print it. So about 650-750 characters of SQL messages were enough to exceed the column.
- The error row is written on every failed attempt of a code file, also on the first one that a retry would fix: a long error there failed the deployment that the retry would have finished.
- An older DbLive reads `error` only as a string and never interprets it: `MinCompatibleVersion` stays.
- `GetSqlServerError` put three tabs after the message of each error (the indentation of a raw string literal); removed, the text is now stored.

## Result
- `CodeItemDeployer` and `CodeBreakingDeployer` store `GetSqlErrorOrMessage()` (`ExceptionExtensions`): `DbLiveSqlException.SqlError`, or the message of the exception when there is no error of the server. The same text as in the exception of a failed migration; `MigrationItemDeployer` now uses the same method. The full exception stays in `CodeDeploymentException` and in the log of `CodeDeployer`.
- `MsSqlDA.FitErrorColumn` cuts the text written to `error` (code, test, migration) to 4000 characters, ending with `... [truncated]`, without splitting a surrogate pair. PostgreSQL (`text`) is not cut.
- No self-migration, `MinCompatibleVersion` unchanged.
- Tests: `CodeItemDeployerTests` and `CodeBreakingDeployerTests` (the stored text), `MsSqlDAFitErrorColumnTests` (the cut), `ErrorColumnTests` on SQL Server: the stored error of a code file with its line, a code file with an error of about 7000 characters fails with `CodeDeploymentAggregateException` and stores 4000 characters, a test with 4200 characters of values fails the run with `UnitTestsFailedException` and stores 4000 characters.
