---
type: bug
priority: low
created: 2026-11-09
---
# The primary key of the table type fails on accent-insensitive collations (SQL Server)

## Problem
`dblive.tvp_unique_string_list` has a `primary key` on `value`, which compares by the collation of the database. Paths that differ only by accent or width (`Code/café.sql` and `Code/cafe.sql`) are different for DbLive: `ScriptPathsDifferOnlyByCaseException` compares with `OrdinalIgnoreCase`, and `ToStoredPath` keeps them apart. Under `Latin1_General_CI_AI` they are equal. `ToPathList` adds both, and `MarkCodeAsVerifiedAsync` and `MarkMissingCodeAsRemovedAsync` fail with "Violation of PRIMARY KEY constraint" on every deployment.

In 1.5 such files shared one row of `dblive.code` (its key `relative_path` compares by the same collation), and the deployment did not fail.

Rare: accent-insensitive or width-insensitive database collations are uncommon, and so are such pairs of file names. From the code, not verified.

Found by the code review of 2026-11-09 (finding 6).

## Where
- `src/DbLive.MSSQL/SQL/13.tvp-unique-string-list.sql:8`
- `src/DbLive.MSSQL/MsSqlDA.cs:505-516` (`ToPathList`)

## Fix
Drop the primary key: the queries use the list in a `join` and in `not exists`, and both work with duplicates. A binary collation on the column instead would make the comparison with `dblive.code.relative_path` fail with a collation conflict, unless every query adds `collate`.

2.0 is not released, so self-migration 13 can still be changed in place. 0096-69 changes the same type: decide both together.

## Done when
- [ ] Reproduced on a database with `Latin1_General_CI_AI`, or ruled out
- [ ] If real: fixed, with an integration test with two such files
