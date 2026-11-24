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
- `src/DbLive.MSSQL/MsSqlDA.cs:896-906` (`PathsTableSql`), `:908-952` (the three methods)
- `src/DbLive.MSSQL/SQL/01.migrations.sql:44-45` (`dblive.version.one_row_lock`, see Notes)

## Fix
Compare with the collation of the column: `#paths` created with the collation of `dblive.code.relative_path` (read from `sys.columns`), or `c.relative_path collate database_default = p.value` in the joins (the table is small; check that the index on `#paths` is still used). The paths are lower case already, so the case sensitivity of the collation does not matter (0096-70).

## Done when
- [x] Fixed, with a test: a database deployed, its collation changed, deployed again with a changed and a removed code file

## Notes
- Found by the code review of 2026-11-24.
- Verified on 2026-11-24 (SQL Server 2025 RTM-CU11, 17.0.5065.9, server collation `SQL_Latin1_General_CP1_CI_AS`; Microsoft.Data.SqlClient of the solution), with a temporary test class in `DbLive.MSSQL.Tests`, removed after:
  - `ALTER DATABASE ... COLLATE` fails on every database deployed by DbLive: Msg 5075 "The column 'version.one_row_lock' is dependent on database collation. The database collation cannot be changed if a schema-bound object depends on it." SQL Server takes every computed column as dependent on the collation, also `as 1` and `as 1 persisted not null`; a `check` constraint blocks it too (`one_row_lock bit not null default 1 check ( one_row_lock = 1 )`). It is the only such object in the `dblive` tables (`sys.computed_columns`, `sys.check_constraints`, schema-bound modules). The column comes from self-migration 01, so it is in every database since DbLive 1.x. With `one_row_lock bit not null default 1` as the primary key, without a `check`, the collation changes.
  - So the bug of this task is reached only after the operator removes this dependency, as the message of SQL Server says: drops `pk_dblive_version` and `one_row_lock`, changes the collation, creates both again.
  - Then, a database created with `Latin1_General_100_CI_AS` and changed to `SQL_Latin1_General_CP1_CI_AS` (`dblive.code.relative_path` keeps `Latin1_General_100_CI_AS`): an unchanged project fails in `MarkCodeAsVerifiedAsync`, changed files in `MarkCodeAsApplyingAsync`, a removed file in `MarkMissingCodeAsRemovedAsync`, all with Msg 468. Also a project without code files fails in `MarkMissingCodeAsRemovedAsync` (it runs on every deployment with `DeployCode`, and the conflict is a compile error, whatever the rows), so every `Default` deployment fails, not only one with code.
  - Not affected after the change: a deployment with `DeployCode = false` (migrations, SQL tests), the breaking phase.
  - Fix candidates, run on the changed database with 3001 rows in `dblive.code` and 2000 paths, also in a snapshot transaction (`TransactionIsolationLevel` `Snapshot` with `Deployment`, 0096-164): `c.relative_path collate database_default` in the three joins works; `#paths` by `select top (0) relative_path as value into #paths from dblive.code` works too and takes the collation and `not null` of the column, but is a heap (`create index` is not allowed in a snapshot transaction, 0096-164). Plan, before and after the change of the collation: both clustered indexes are scanned, with and without `collate database_default`, so the `collate` loses no seek.
- In 1.5 the code commands compared `dblive.code.relative_path` with parameters, which take the collation of the column: no conflict. `#paths` came with 0096-69, not released, so this is a regression of 2.0 before its release.
- Decision of the maintainer, 2026-11-24: `c.relative_path collate database_default` in the three queries. `one_row_lock` stays as it is: whoever changes the collation of a database removes the dependencies of their own objects anyway (computed columns, `check` constraints, schema-bound modules), and `dblive.version` is one more of them. Not chosen: `#paths` by `select into` (no index, against 0096-69 and 0096-164), a plain `one_row_lock` column (little gain), no fix.
- Test: `DatabaseCollationChangeTests` (SQL Server): a database created with `Latin1_General_100_CI_AS`, deployed, changed to `SQL_Latin1_General_CP1_CI_AS` the way the message of SQL Server asks (`one_row_lock` dropped and created again), deployed with an unchanged, a changed and a removed code file. Verified: without the fix it fails with Msg 468 in `MarkCodeAsVerifiedAsync`.
