---
type: bug
priority: medium
created: 2026-11-08
---
# A long error text can fail the write of the error row on SQL Server

## Problem
On SQL Server the column `error` of `dblive.code`, `dblive.unit_test_run` and `dblive.migration` is `nvarchar(4000)` (PostgreSQL: `text`). DbLive writes there:

- `ex.ToString()` of a failed code file (`CodeItemDeployer`) and of a failed breaking script of the Code folder (`CodeBreakingDeployer`), outside a transaction: the message, the SQL error and the stack traces of `DbLiveSqlException`, SMO and SqlClient.
- the error message of a failed test (`UnitTestsRunner`), which for a comparison of result sets lists the rows.

A text over 4000 characters makes the insert fail with "String or binary data would be truncated ... column 'error'", and that exception leaves the deployment (or `RunAllTestsAsync`) instead of the original error: no file name and no SQL error. For a test, `RunAllTestsAsync` fails with `SqlException` instead of `UnitTestsFailedException`.

Not verified: the length of `ex.ToString()` was estimated from the stack, not measured. Example to try: a view with many invalid columns, or a test whose result sets have wide rows.

Found by the code review of 2026-11-08 (finding 3).

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:71`
- `src/DbLive/Deployers/Code/CodeBreakingDeployer.cs:59`
- `src/DbLive/Deployers/Testing/UnitTestsRunner.cs:49`
- `src/DbLive.MSSQL/SQL/01.migrations.sql`, `02.code.sql`, `03.unit-tests.sql` (the columns)

## Fix
- [ ] Measure first: the length of `ex.ToString()` of a failed code file on SQL Server, and of the message of a failed comparison.
- [ ] Which fix: a self-migration that makes the three columns `nvarchar(max)`; cutting the text to 4000 characters in `MsSqlDA`; or a shorter text (the message and the SQL error, without stack traces)? Widening a column changes nothing for an older DbLive, so `MinCompatibleVersion` stays (to confirm against "Compatibility of the `dblive` tables" in `Development.md`).

## Docs to update
- `docs/llms-full.txt` §15, if the column type or the stored text changes

## Done when
- [ ] Reproduced, or closed as not reproducible with the measured lengths
- [ ] Fixed, with an integration test on SQL Server: a code file and a test whose error is longer than 4000 characters
- [ ] Docs updated
