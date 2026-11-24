---
type: bug
priority: low
created: 2026-11-24
---
# SQL Server: the code deployment fails with a collation conflict after `ALTER DATABASE ... COLLATE`

## Problem
`#paths.value` is `collate database_default`, the collation of the database now. `dblive.code.relative_path` keeps the collation the column was created with. After `ALTER DATABASE ... COLLATE` (the columns of existing tables keep theirs) the joins of `#paths` with `dblive.code` fail with Msg 468 `Cannot resolve the collation conflict between "SQL_Latin1_General_CP1_CI_AS" and "Latin1_General_100_CI_AS" in the equal to operation`, and every deployment with code fails in `MarkCodeAsVerifiedAsync`, `MarkCodeAsApplyingAsync` or `MarkMissingCodeAsRemovedAsync`. Verified by the review on 2026-11-24 on SQL Server 2025 with the SQL of these methods.

A change of the collation of a database is rare, and not a mistake: DbLive should keep working after it.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:876-885` (`PathsTableSql`), `:887-931` (the three methods)

## Fix
Compare with the collation of the column: `#paths` created with the collation of `dblive.code.relative_path` (read from `sys.columns`), or `c.relative_path collate database_default = p.value` in the joins (the table is small; check that the index on `#paths` is still used). The paths are lower case already, so the case sensitivity of the collation does not matter (0096-70).

## Done when
- [ ] Fixed, with a test: a database deployed, its collation changed, deployed again with a changed and a removed code file

## Notes
- Found by the code review of 2026-11-24.
