---
type: bug
priority: low
created: 2026-11-09
closed: 2026-11-14
---
# The primary key of the table type fails on accent-insensitive collations (SQL Server)

## Release note
None. The table type came with 0096-44 and was never released.

## Problem
`dblive.tvp_unique_string_list` has a `primary key` on `value`, which compares by the collation of the database. Paths that differ only by accent or width (`Code/café.sql` and `Code/cafe.sql`) are different for DbLive: `ScriptPathsDifferOnlyByCaseException` compares with `OrdinalIgnoreCase`, and `ToStoredPath` keeps them apart. Under `Latin1_General_CI_AI` they are equal. `ToPathList` adds both, and `MarkCodeAsVerifiedAsync` and `MarkMissingCodeAsRemovedAsync` fail with "Violation of PRIMARY KEY constraint" on every deployment.

In 1.5 such files shared one row of `dblive.code` (its key `relative_path` compares by the same collation), and the deployment did not fail.

Rare: accent-insensitive or width-insensitive database collations are uncommon, and so are such pairs of file names. Verified, see Notes.

Found by the code review of 2026-11-09 (finding 6).

## Where
- `src/DbLive.MSSQL/SQL/13.tvp-unique-string-list.sql:8`
- `src/DbLive.MSSQL/MsSqlDA.cs:505-516` (`ToPathList`)

## Fix
Drop the primary key: the queries use the list in a `join` and in `not exists`, and both work with duplicates. A binary collation on the column instead would make the comparison with `dblive.code.relative_path` fail with a collation conflict, unless every query adds `collate`.

2.0 is not released, so self-migration 13 can still be changed in place. 0096-69 changes the same type: decide both together.

Decided with 0096-69: the type is gone, and the temporary table that holds the paths now has an index that is not unique.

## Done when
- [x] Reproduced on a database with `Latin1_General_CI_AI`, or ruled out
- [x] If real: fixed, with an integration test with two such files

## Notes
- Reproduced (verified, SQL Server 2025): a database created with `collate Latin1_General_CI_AI` and a project with `Code/café.sql` and `Code/cafe.sql`; the first deployment fails in `MarkMissingCodeAsRemovedAsync` with "Violation of PRIMARY KEY constraint '...'. Cannot insert duplicate key in object 'dbo.@paths'. The duplicate key value is (code/café.sql)."
- 0096-69 replaced the table-valued parameter with xml read into the temporary table `#paths`. Its clustered index is not unique, so the two paths are two rows, and the `join` and the `not exists` work with them. The column has `collate database_default`, which the database of this test needs: the server collation is `SQL_Latin1_General_CP1_CI_AS` (0096-69, Notes).

## Result
- Fixed by 0096-69: no table type, and `#paths` has an index that is not unique.
- Test: `AccentInsensitiveCollationTests` (SQL Server): a database with `Latin1_General_CI_AI`, two code files that differ only by accent, two deployments. It failed before the fix with the error in Notes.
