---
type: bug
priority: low
created: 2026-11-12
closed: 2026-11-17
breaking: true
---
# Small fixes from the code review of 2026-11-12

## Release note
**`rows-with-schema` compares the SQL Server types it did not know.** `time` columns are compared with their scale (`time(3)` is not `time(7)`), and `json`, `vector`, `geography` and `hierarchyid` by their name. Before, all of them had the type `unknown` and were equal to each other. A `rows-with-schema` test that compared `time` columns of different scales, or two of these types, now fails: cast the expected value to the type of the actual column, for example `cast('10:00' as time(3))`. A failed test shows `nvarchar(max)`, `varchar(max)` and `varbinary(max)` instead of `nvarchar(2147483647)`.

**`NaN` and infinity in PostgreSQL tests.** In `rows` and `rows-with-schema`, `NaN`, `Infinity` and `-Infinity` of `real` and `double precision` columns equal themselves. Before, the test failed with `.NET number values such as positive and negative infinity cannot be written as valid JSON`.

## Problem
Two small defects in the comparison of result sets in SQL tests. Both verified with a temporary harness (not kept), on SQL Server 2022 and PostgreSQL 18, and again on SQL Server 2025 with Microsoft.Data.SqlClient 6.1.4 and Npgsql 10.0.1.

### 1. `rows-with-schema` does not compare `time` columns and other types it does not know (SQL Server)
`MsSqlDA.GetSqlTypeName` has no case for `SqlDbType.Time`, `Udt` (`geography`, `geometry`, `hierarchyid`, CLR types of the user), and the `Json` and `Vector` of SQL Server 2025: their type is `unknown`. Two columns of type `unknown` are equal, so the assertion passes for different types: `time(3)` against `time(7)`, and `time` against `json` too. (`Structured`, a table-valued parameter, never comes in a result set.)

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

PostgreSQL only: a `float` of SQL Server holds no `NaN` and no infinity.

Found by the code review of 2026-11-12 (finding 6).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:435-484` (`GetSqlColumn`, `GetSqlTypeName`)
- `src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:310-346` (`CompareRows`)

## Fix
1. Add `SqlDbType.Time` as `time(<scale>)`. For a type without a case, use the type name the reader gives (`DataTypeName` of the schema table) instead of `unknown`, so two different types never compare as equal: `json`, `vector`. PostgreSQL does the same already (`PostgreSqlDA.GetPostgresTypeName`). A CLR type (`Udt`) by its own name, without the database and the schema: `DataTypeName` gives `master.sys.geography`, the type is `geography` (decision of 2026-11-17). Write the `max` types as `max` when `ColumnSize` is `int.MaxValue`.
2. Serialize with `JsonNumberHandling.AllowNamedFloatingPointLiterals`: `NaN` equals `NaN`, `Infinity` equals `Infinity`. One `JsonSerializerOptions` for all comparisons, not a new one per row.

## Docs to update
- `docs/llms-full.txt` §8.3 (value comparison: `NaN` and infinity, `numeric` `NaN`, CLR types of SQL Server; the type names of `rows-with-schema`), §17 (the errors of `numeric` `NaN` and of `Microsoft.SqlServer.Types`)
- `docs/unit-testing/index.md` ("Assertions", "How values are compared")

## Done when
- [x] `rows-with-schema` fails for `time(3)` against `time(7)`, and shows `nvarchar(max)`; integration test on SQL Server
- [x] A test that compares `NaN` with `NaN` passes; unit test
- [x] Docs updated

## Notes
- Verified on SQL Server 2025 (SqlClient 6.1.4), the schema table of the reader: `time(3)` and `time(7)` have `ProviderType` `Time`, `NumericScale` 3 and 7, `DataTypeName` `time`. `nvarchar(max)`, `varchar(max)`, `varbinary(max)`: `ColumnSize` 2147483647. `geography` and `hierarchyid`: `ProviderType` `Udt`, `DataTypeName` `master.sys.geography`, `master.sys.hierarchyid` (connected to `master`). `json`: `ProviderType` `Json` (35), `DataTypeName` `json`; `vector(3)`: `ProviderType` `Vector` (36), `DataTypeName` `vector`, `ColumnSize` 20.
- `vector` is written without its dimension: `vector(3)` and `vector(4)` have the same type name, but their values differ anyway.
- Verified on PostgreSQL 18 (Npgsql 10.0.1): `'NaN'::float8` is read as `double.NaN`, `array['NaN'::float8, 1]` as `double[]`. With `AllowNamedFloatingPointLiterals` System.Text.Json writes `"NaN"` and `["NaN",1]`: the option covers arrays too, a case for `double` in the comparison would not.
- `NaN` is written as the JSON string `"NaN"`, so in `rows` the `float8` `NaN` equals the text `'NaN'`. Accepted, decision of 2026-11-17: `rows` does not compare types (`int` 10 equals `bigint` 10), and `rows-with-schema` tells them apart. A converter that writes a bare `NaN` token was the alternative.
- Not fixed, decision of 2026-11-17: `'NaN'::numeric` and `'Infinity'::numeric` fail before the comparison, when Npgsql reads the value: `InvalidCastException: Numeric NaN not supported by System.Decimal`, `OverflowException: Numeric value does not fit in a System.Decimal` (verified). Rare, and the error says what is wrong. A known limitation in the docs.
- `breaking: true`: a `rows-with-schema` test that compared `time(0)` with `time(7)`, or two other types that were both `unknown`, passed and now fails. `docs/versioning.md`: "A fix that makes a wrong result fail counts too". The release is 2.0.0 anyway.
- Verified on SQL Server 2025: `DataTypeName` of a CLR type names the current database (`tempdb.sys.geography` when connected to `tempdb`), and reading a `geography` or `hierarchyid` value fails with `FileNotFoundException: Could not load file or assembly 'Microsoft.SqlServer.Types, Version=10.0.0.0, ...'`: DbLive does not reference the package. Not fixed: a known limitation in the docs, with `.ToString()` of the value as the way around it. So the type names of CLR types matter only to a test project that references `Microsoft.SqlServer.Types` (not verified with the package).
- The messages of a failed test hold the line ends of `UnitTestResultChecker.cs` (CRLF in a Windows working copy, LF on CI), and `SqlTestResultSetTests.cs` of both providers is LF: the new tests compare ignoring line ends.
- Verified: the new tests fail without the fix (the six cases of the unit tests throw the JSON error, the two SQL Server tests and the PostgreSQL test fail) and pass with it.

## Result
- `MsSqlDA.GetSqlTypeName` takes the `DataTypeName` of the schema table too: `time(<scale>)`; a CLR type (`Udt`) by the last part of its name (`geography`, `hierarchyid`); a type without a case by its name (`json`, `vector`), never `unknown`. The `max` types are `nvarchar(max)`, `varchar(max)`, `varbinary(max)` when `ColumnSize` is `int.MaxValue` (`MaxColumnSize`).
- `UnitTestResultChecker`: one static `JsonSerializerOptions` with `JsonNumberHandling.AllowNamedFloatingPointLiterals` for all value comparisons.
- Decisions of 2026-11-17: CLR types by their own name; the `float8` `NaN` equal to the text `'NaN'` in `rows` is accepted; `numeric` `NaN` and infinity are not fixed, a known limitation in the docs.
- Docs: `docs/llms-full.txt` §8.3 (type names of `rows-with-schema`; `NaN` and infinity; `numeric` `NaN`; `geography`, `geometry` and `hierarchyid` values need `Microsoft.SqlServer.Types`) and §17 (two rows: the errors of `numeric` `NaN` and of `Microsoft.SqlServer.Types`); `docs/unit-testing/index.md` ("Assertions", "How values are compared").
- Tests: `UnitTestResultCheckerRowsTests.Rows_NaN_and_infinity_equal_themselves` (`NaN`, `Infinity`, `-Infinity` as `double`, `float` and inside a `double[]`) and `Rows_NaN_and_infinity_differ_from_other_values`. SQL Server (2025 container): `SqlTestResultSetTests.Column_types_are_named_as_in_TSql` (`time(3)`, `time(7)`, the three `max` types, `geography`, `hierarchyid`, `json`, `vector`) and `Rows_with_schema_test_fails_for_time_of_another_scale` (the message shows `time(7)` against `time(3)` and `nvarchar(max)`). PostgreSQL: `SqlTestResultSetTests.Rows_test_compares_NaN_and_infinity` (`float8`, `float4` and an array pass `rows-with-schema`, `NaN` against 1 fails with a row message).
- All tests pass: `DbLive.Tests` 530, `DbLive.MSSQL.Tests` 130, `DbLive.PostgreSQL.Tests` 62, the demo tests (`DemoMSSQL.Tests` 13, `AdventureWorks.Tests` 5, `Demo.PostgreSQL.Chinook.Tests` 6).
