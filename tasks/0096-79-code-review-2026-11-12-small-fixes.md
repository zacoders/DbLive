---
type: bug
priority: low
created: 2026-11-12
---
# Small fixes from the code review of 2026-11-12

## Problem
Two small defects in the comparison of result sets in SQL tests. Both verified with a temporary harness (not kept), on SQL Server 2022 and PostgreSQL 18.

### 1. `rows-with-schema` does not compare `time` columns and other types it does not know (SQL Server)
`MsSqlDA.GetSqlTypeName` has no case for `SqlDbType.Time`, and none for `Udt` and `Structured`: their type is `unknown`. Two columns of type `unknown` are equal, so the assertion passes for different types.

Verified: this test passes.

```sql
select cast('10:00' as time(3)) as t;
select 'rows-with-schema' as assert;
select cast('10:00' as time(7)) as t;
```

The same function writes the `max` types with their size in bytes: a failed test shows `nvarchar(2147483647)`, `varchar(2147483647)`, `varbinary(2147483647)` (verified). The code compares `ColumnSize` with `-1`, but SqlClient reports `int.MaxValue`. A message, not a wrong result.

### 2. A `NaN` or infinite value fails the test with a JSON error
`UnitTestResultChecker.CompareRows` serializes each value to JSON. `System.Text.Json` does not write `NaN`, `Infinity` and `-Infinity`, so the comparison throws.

Verified on PostgreSQL: this test fails, with the error `.NET number values such as positive and negative infinity cannot be written as valid JSON. ...`

```sql
select 'NaN'::float8 as v;
select 'rows' as assert;
select 'NaN'::float8 as v;
```

Found by the code review of 2026-11-12 (finding 6).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:289-325` (`GetSqlTypeName`)
- `src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:310-346` (`CompareRows`)

## Fix
1. Add `SqlDbType.Time` as `time(<scale>)`. For a type without a case, use the type name the reader gives (`DataTypeName` of the schema table) instead of `unknown`, so two different types never compare as equal. Write the `max` types as `max` when `ColumnSize` is `int.MaxValue`.
2. Serialize with `JsonNumberHandling.AllowNamedFloatingPointLiterals`: `NaN` equals `NaN`, `Infinity` equals `Infinity`.

## Docs to update
- `docs/llms-full.txt` §8.3 (value comparison: `NaN` and infinity; the type names of `rows-with-schema`)

## Done when
- [ ] `rows-with-schema` fails for `time(3)` against `time(7)`, and shows `nvarchar(max)`; integration test on SQL Server
- [ ] A test that compares `NaN` with `NaN` passes; unit test
- [ ] Docs updated
