---
type: bug
priority: high
created: 2026-11-03
closed: 2026-11-03
---
# SQL Server: hidden key columns in the result sets of a SQL test

## Release note
SQL Server: a `rows` or `rows-with-schema` test whose actual result set does not select the primary key of its table no longer fails with `Columns does not match` when the data matches. DbLive read the result sets with `CommandBehavior.KeyInfo`, and SQL Server added the key columns of the table to them as hidden columns. Nothing to change on upgrade.

## Problem
On SQL Server, a `rows` or `rows-with-schema` test whose actual result set does not select the primary key of its table got an extra column, so it failed although the data matched:

```sql
exec dbo.GetUserName @UserId = 10;   -- select Name from dbo.Users where UserId = @UserId
select assert = 'rows';
select 'TestUser10' as Name;
```

```
Columns does not match:
Expected columns: Name: varchar(10)
Actual columns:   Name: nvarchar(50), UserId: int
```

`ExecuteQueryMultipleAsync` read the result sets with `CommandBehavior.KeyInfo`. With it, SqlClient turns on browse mode (`SET NO_BROWSETABLE ON`) for the whole batch, and SQL Server adds the key columns of the base table to the result set as hidden columns. `ReadResultAsync` took every row of `GetSchemaTable()` as a column and read `FieldCount` values, hidden ones included.

Verified 2026-11-03 against a local SQL Server with Microsoft.Data.SqlClient 6.1.4 (the version DbLive uses; the first check was with System.Data.SqlClient, same result), on a table `(UserId int identity primary key, Name nvarchar(50))`:

- `KeyInfo`, `select Name from ...` → `FieldCount=2 VisibleFieldCount=1: Name, UserId (IsHidden=true)`; the same from a procedure.
- `KeyInfo`, `select distinct Name from ...` and `select count(*) ...` → no hidden column.
- Without `KeyInfo` → `FieldCount=1: Name`, with the same `ProviderType` and `ColumnSize`; `IsHidden` is `DBNull`.
- The failure message above: verified by `SqlTestResultSetTests` before the fix.

Every test in the repository selected the key column, so none hit it. `has-rows`, `row-count`, `empty` and `single-row` count rows only, so they were not affected; the typo guard reads the first column only.

PostgreSQL is not affected: `PostgreSqlDA` also reads with `CommandBehavior.KeyInfo`, but Npgsql does not add columns to the result set (verified by the PostgreSQL `SqlTestResultSetTests`).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs`: `ExecuteQueryMultipleAsync` (`ExecuteReaderAsync(CommandBehavior.KeyInfo)`).

## Fix
- [x] Read without `CommandBehavior.KeyInfo`. Nothing reads what `KeyInfo` adds (`IsKey`, `BaseTableName`); `GetSqlColumn` needs `ProviderType`, `NumericPrecision`, `NumericScale` and `ColumnSize`, which the schema table has without it. Chosen over skipping `IsHidden` columns: that keeps browse mode on for the code under test, for nothing.
- [x] Test: a `rows` test on a select without the key column passes; one with `rows-with-schema` too.
- [x] Does it change a test that passes today? Only in a contrived case, see Notes, so `breaking` is not set.

## Docs to update
- None: the fix makes the behavior match `docs/llms-full.txt` §8.3 (same column count and names).

## Done when
- [x] Fixed, with a test on SQL Server
- [x] Release note

## Notes
Found in the documentation review of #96 (2026-11-03).

`KeyInfo` came with the column types of `rows-with-schema` in 0d065a6 (2025-02-24); nothing in the history says why, and nothing uses the key info.

The expected side can get hidden columns too, when it selects from a table: `select Name from dbo.Users` as the expected result set gave `Name, UserId`. So one kind of test passed before and fails now: expected `select Name from T`, actual `select Name, UserId from T`. Such a test relied on a column nobody sees, so it is not treated as breaking. Tests where both sides select from tables without the key passed before (both had the hidden column) and still pass.

PostgreSQL keeps `CommandBehavior.KeyInfo`. Npgsql probably uses it to read the column details from the catalog, so dropping it could change the types `rows-with-schema` sees. Not checked: out of scope, PostgreSQL is not affected by this bug.

## Result
`MsSqlDA.ExecuteQueryMultipleAsync` reads the result sets without `CommandBehavior.KeyInfo`, so a result set holds the selected columns only. Tests: `SqlTestResultSetTests` in `DbLive.MSSQL.Tests` (failed before the fix, pass after) and the same in `DbLive.PostgreSQL.Tests`: the columns of a select without the key column, directly and from a procedure, and a `rows` and a `rows-with-schema` test on it. The whole solution passes (512 tests, the demo projects included). No docs change.
