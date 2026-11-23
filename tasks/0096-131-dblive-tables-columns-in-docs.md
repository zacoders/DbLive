---
type: docs
priority: low
created: 2026-11-23
---
# §15 lists the columns of the `dblive` tables incompletely, and `dblive.migration.error` is always NULL

## Problem
`docs/llms-full.txt` §15 (lines 1005-1007):
- `dblive.code` misses `created_utc` (SQL Server `src/DbLive.MSSQL/SQL/02.code.sql:7`, PostgreSQL `SQL/01.migration.sql`), which `SaveCodeItemAsync` writes.
- `dblive.unit_test_run` misses `content_hash` (SQL Server `SQL/03.unit-tests.sql:4`, PostgreSQL `SQL/01.migration.sql`), which `SaveUnitTestResultAsync` writes.
- `dblive.migration` lists `error`, but it is always NULL: `MigrationItemDeployer` writes `ErrorMessage = null` (`src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:111`), and a failed migration writes no row. 0096-60 noted it; the docs do not say it. A user who runs `select ... error from dblive.migration` after a failed migration gets nothing.

## Fix
Add the two columns. For `dblive.migration.error`:

- [ ] Document that it stays NULL (the error is in the exception and the log), write the error of a failed migration there, or drop the column (a self-migration; `MinCompatibleVersion` is 2.0.0 already)?

## Docs to update
- `docs/llms-full.txt` §15

## Done when
- [ ] Docs updated (and the code, if the decision changes it)
