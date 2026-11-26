---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# §15 lists the columns of the `dblive` tables incompletely, and `dblive.migration.error` is always NULL

## Release note
None. Documentation only; the columns `error` are dropped by 0096-141.

## Problem
`docs/llms-full.txt` §15 (lines 1005-1007):
- `dblive.code` misses `created_utc` (SQL Server `src/DbLive.MSSQL/SQL/02.code.sql:7`, PostgreSQL `SQL/01.migration.sql`), which `SaveCodeItemAsync` writes.
- `dblive.unit_test_run` misses `content_hash` (SQL Server `SQL/03.unit-tests.sql:4`, PostgreSQL `SQL/01.migration.sql`), which `SaveUnitTestResultAsync` writes.
- `dblive.migration` lists `error`, but it is always NULL: `MigrationItemDeployer` writes `ErrorMessage = null` (`src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:111`), and a failed migration writes no row. 0096-60 noted it; the docs do not say it. A user who runs `select ... error from dblive.migration` after a failed migration gets nothing.

## Fix
Add the two columns. For `dblive.migration.error`:

- [x] Document that it stays NULL (the error is in the exception and the log), write the error of a failed migration there, or drop the column (a self-migration; `MinCompatibleVersion` is 2.0.0 already)? Decided by the maintainer on 2026-11-23 in 0096-141: the `dblive` tables keep only successes, and the column is dropped there.

## Docs to update
- `docs/llms-full.txt` §15

## Done when
- [x] Docs updated (and the code, if the decision changes it)

## Result
- 0096-141 dropped `dblive.migration.error`, `dblive.code.error` and `dblive.unit_test_run` with its `content_hash`, so only `dblive.code.created_utc` was left: §15 now lists it (when the row was written first, the first successful run of the path; a later run does not change it).
