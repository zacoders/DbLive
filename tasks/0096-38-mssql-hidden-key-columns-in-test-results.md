---
type: bug
priority: high
created: 2026-11-03
---
# SQL Server: hidden key columns in the result sets of a SQL test

## Problem
On SQL Server, a `rows` or `rows-with-schema` test whose actual result set does not select the primary key of its table gets an extra column, so it fails although the data matches:

```sql
exec dbo.GetUserName @UserId = 10;   -- select Name from dbo.Users where UserId = @UserId
select assert = 'rows';
select 'TestUser10' as Name;
```

`ExecuteQueryMultipleAsync` reads the result sets with `CommandBehavior.KeyInfo`. With it, SqlClient turns on browse mode (`SET NO_BROWSETABLE ON`), and SQL Server adds the key columns of the base table to the result set as hidden columns. `ReadResultAsync` takes every row of `GetSchemaTable()` as a column and reads `FieldCount` values, hidden ones included.

Verified 2026-11-03 against a local SQL Server, with `System.Data.SqlClient` and `CommandBehavior.KeyInfo`, on a table `(UserId int identity primary key, Name nvarchar(50))`:

- `select Name from dbo.KeyInfoProbe;` → `FieldCount=2 VisibleFieldCount=1: Name(hidden=False), UserId(hidden=True)`
- `exec dbo.KeyInfoProbeProc;` (the same select in a procedure) → the same.

Not verified yet: the failure message of the test (expected: the column count or the column names differ). Every test in the repository selects the key column, so none hits it. `has-rows`, `row-count`, `empty` and `single-row` count rows only, so they are not affected; the typo guard reads the first column only.

PostgreSQL is not affected (`PostgreSqlDA` does not use `KeyInfo`).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs`: `ExecuteQueryMultipleAsync` (`ExecuteReaderAsync(CommandBehavior.KeyInfo)`) and `ReadResultAsync` (all schema rows, `reader.FieldCount`).

## Fix
- [ ] Read without `CommandBehavior.KeyInfo`, or skip the columns with `IsHidden = true` and read `VisibleFieldCount` values. Check what `KeyInfo` is used for first (`GetSqlColumn` needs `ProviderType`, `NumericPrecision`, `NumericScale`, `ColumnSize`, which the schema table has without it).
- [ ] Test: a `rows` test on a select without the key column passes; one with `rows-with-schema` too.
- [ ] Does it change a test that passes today? A test that expects the hidden column cannot exist (the expected side never has it), so probably not; then `breaking` is not needed.

## Docs to update
- None, if the fix makes the behavior match `docs/llms-full.txt` §8.3 (same column count and names).

## Done when
- [ ] Fixed, with a test on SQL Server
- [ ] Release note

## Notes
Found in the documentation review of #96 (2026-11-03).
