---
type: bug
priority: medium
created: 2026-11-24
closed: 2026-11-24
---
# PostgreSQL: a SQL test fails on an array of numbers, dates or ranges with a NULL element

## Release note
**PostgreSQL: a SQL test reads an array with a NULL element.** An array of numbers, dates, times, ranges, `uuid`, `cidr` or a geometric type with a NULL element, such as `array_agg` of a nullable column returns, failed the test whatever it asserted: `Cannot read a non-nullable collection of elements because the returned array contains nulls. Call GetFieldValue with a nullable collection type instead.` Now `rows` compares it with `null` for the NULL element: `[10,null,30]` equals `array[10, null, 30]`. Nothing to change on upgrade.

## Problem
Npgsql reads an array of a .NET value type with `GetValue` as an array of non-nullable elements (`int[]`), and throws when the array has a NULL element. DbLive reads every value of every result set with `GetValue` (`PostgreSqlDA.ReadValue`), so a SQL test with such a value fails, with any assert and without one:

`Cannot read a non-nullable collection of elements because the returned array contains nulls. Call GetFieldValue with a nullable collection type instead.`

The message tells the user to call a method that only DbLive calls. `array_agg` of a nullable column gives such a value.

Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18` (0096-154): `array[1, null]::int[]`, `array[true, null]`, `array[1.5, null]::numeric[]`, `array['2026-01-01'::date, null]`, `array['(0,0)'::point, null]`, `array['[1,2)'::int4range, null]` and `array['((0,0),(1,1))'::path, null]` fail. `array['a'::text, null]` is read, as `string[]` with a `null`; so is `inet[]` (0096-145) and, since 0096-154, `tsquery[]` and `tsvector[]`.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`, `ReadValue`

## Fix
Approach 1 of Notes (the maintainer, 2026-11-24):
- When `GetValue` fails on an array whose element (`PostgresArrayType.Element.InternalName`) has a .NET value type, read it as `GetFieldValue<T?[]>` by a map of the element name to `T`. When that fails too, the error of `GetValue` stays.
- `ReadValue` writes a `NpgsqlPath?[]` as text too, as `NpgsqlPath[]` (0096-154): also for `Array Nullability Mode` in the connection string of the user.
- Docs: also `tid[]` and `pg_lsn[]`, which cannot be read, and `timestamp[]` under `Array Nullability Mode=PerInstance`.

- [x] Does the text of `rows` stay the same for an array without NULL: `int?[]` `[1,2]` against `int[]` `[1,2]`? Yes, for every value type of the map in Notes: the text of `T?[]` without NULL is the text of `T[]`, and a NULL element is `null` (`[1,null]`), the text that `Array Nullability Mode=Always` gives too (verified, Notes). But `GetFieldType` cannot choose `T`: for an array it is `System.Array` (Notes).

## Docs to update
- `docs/llms-full.txt` §8.3, §17
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [x] Fixed, with a test: arrays of `int`, `numeric`, `date` and a range with a NULL element are compared in `rows`
- [x] A test that the map gives the element types of `GetValue`
- [x] An open `path` differs from the closed one in an array with a NULL element, also with `Array Nullability Mode=Always`
- [x] Docs updated

## Notes
- Found in 0096-154: a `path[]` with a NULL element failed the read before `ReadValue` got to it.
- Since 0096-166 (2026-11-24) the error names the column and ends with `Select the value as text in the test.`: `Cannot read the value of the column 'v' in row 1 of result set 1, of the type integer[]: Cannot read a non-nullable collection ...`. `docs/llms-full.txt` §8.3 and §17 list such an array among the values that cannot be read: take it out there when it is read.
- Experiments of 2026-11-24 (verified), Npgsql 10.0.1, `postgres:18` in Docker, net8.0, a temporary test class in `DbLive.PostgreSQL.Tests` (removed). Each value as `select <value> as v`, through `IDbLiveDA.QuerySqlAsync` (so `PostgreSqlDA.ReadValue`) and through plain Npgsql, written with `RowValueText.Write`.
  - Through DbLive, with a NULL element, fail with the error of 0096-166: `int2`, `int4`, `int8`, `numeric`, `float4`, `float8`, `bool`, `money`, `oid`, `xid`, `xid8`, `"char"`, `date`, `time`, `timetz`, `timestamp`, `timestamptz`, `interval`, `uuid`, `point`, `lseg`, `box`, `line`, `circle`, `path`, `polygon`, `int4range`, `int8range`, `numrange`, `daterange`, `tsrange`, `tstzrange`, `cidr`, `regtype`, a domain over `int` (the server sends the base type, `integer[]`), an array with other bounds (`'[0:1]={1,NULL}'::int4[]`), a two-dimensional one, and `select array_agg(m order by id) from t` of a nullable column. Read with a NULL element: `text`, `inet`, `macaddr`, `bytea`, `jsonb`, `tsquery`, `bit(1)` and `bit(3)` (`BitArray[]`), an enum (`string[]`), `int4multirange` (an array of arrays). The problem of the task is reproduced on the current code.
  - The planned fix does not work as written: `reader.GetFieldType(ordinal)` and `NpgsqlDbColumn.DataType` of every array are `System.Array`, without the element type. `GetFieldValue<object[]>` and `GetFieldValue<List<object>>` throw `NotSupportedException`, `GetFieldValue<IList>` and `GetFieldValue<Array>` fail as `GetValue` does. What names the element is `reader.GetPostgresType(ordinal)`: a `PostgresArrayType` whose `Element.InternalName` is `int4`, `float8`, `timestamptz`, `int4range` and so on. No public API of Npgsql maps it to a .NET type.
  - A prototype read the array by a map of `Element.InternalName` to `GetFieldValue<T?[]>` (`int2` `short`, `int4` `int`, `int8` `long`, `numeric` and `money` `decimal`, `float4` `float`, `float8` `double`, `bool`, `oid`/`xid`/`cid`/`regtype`/`regconfig` `uint`, `xid8` `ulong`, `char` `char`, `date` `DateOnly`, `time` `TimeOnly`, `timetz` `DateTimeOffset`, `timestamp`/`timestamptz` `DateTime`, `interval` `TimeSpan`, `uuid` `Guid`, the geometric types their `Npgsql*` struct, the ranges `NpgsqlRange<int|long|decimal|DateOnly|DateTime>`, `cidr` `IPNetwork`). Every one read with a NULL element, and for all 33 the text without NULL equaled the text of `GetValue`, with NULL the text of `GetValue` on a connection with `Array Nullability Mode=Always`. `NaN` and infinity of `float4`/`float8` stay `"NaN"`, `"Infinity"`; `timestamptz` stays UTC (`...Z`). A two-dimensional array fails `GetFieldValue<int?[]>` with `Cannot read an array value with 2 dimensions into a collection type with 1 dimension ...`, also without NULL, where `GetValue` reads it (and `rows` then fails with `Cannot compare ...`, §8.3): so the map may only be tried after `GetValue` fails.
  - `Array Nullability Mode` in the connection string of the user (not set by DbLive): with `Always` every array of a value type is `T?[]`, with `PerInstance` `T?[]` only with a NULL element. Both read the arrays of the task. But then a `path[]` comes as `NpgsqlPath?[]`, and `PostgreSqlDA.ReadValue` matches only `NpgsqlPath[]` (0096-154): the path is written as its points without `Open`, `'((0,0),(1,1))'` and `'[(0,0),(1,1)]'` give the same text, so an open path equals a closed one again (verified: the type and the texts with plain Npgsql and `RowValueText`; the miss of the pattern follows from the code). With `PerInstance` `timestamp[]`, `timestamptz[]`, `tsrange[]` and `tstzrange[]` cannot be read at all, also without NULL: `Reading as 'System.Object' is not supported for fields having DataTypeName 'timestamp without time zone[]'`. One more reason not to set `PerInstance` for the user.
  - Found, not in the docs: `tid[]` and `pg_lsn[]` cannot be read at all, also without NULL: `Reading as 'System.Object' is not supported for fields having DataTypeName 'tid[]'` (the single values read, 0096-154). §8.3 does not list them; the error of 0096-166 says to select them as text.
- How often and how bad: a test of a function that returns an array built from nullable columns (`array_agg`, `array[a, b]`) meets it; not rare, not common. The test fails, so nothing passes by mistake, and since 0096-166 the error names the column and says to select the value as text; the cost is the workaround: the expected value becomes the PostgreSQL text of the array, `'{10,NULL,30}'`.
- Approaches (2026-11-24, for the maintainer):
  1. Read it, by a map. `ReadValue` calls `GetValue` as now; when it throws `InvalidCastException` and the column is a `PostgresArrayType` whose element is in the map above, it reads `GetFieldValue<T?[]>`; when that throws too (a two-dimensional array, a type outside the map), the first error stays. The `path` case matches `NpgsqlPath?[]` too, which also fixes `Array Nullability Mode` of the user. Nothing changes for a value that reads today (verified above). A test of the guard: for each element of the map, `GetValue` of an array without NULL is `T[]`, so an upgrade of Npgsql that changes a default type fails it. Limits: a range of a user-defined subtype is not in the map and keeps the error; an AppContext switch of Npgsql that changes a default type (`Npgsql.EnableLegacyTimestampBehavior` may read `date` as `DateTime`; not verified, it is process-wide) would give an array with NULL the text of the map's type. Effort small to medium (the map of about 30 rows, tests, docs), low risk. Not `breaking`: a test that failed can pass, none that passed changes. No `MinCompatibleVersion`. Docs: §8.3 and §17 take the array out of the values that cannot be read, §8.3 says a NULL element is `null` (`[1,null]`); `docs/unit-testing/index.md`.
  2. Docs only. 0096-166 already documents the array with a NULL element and the workaround (§8.3, §17), so the task would close with small corrections: §17 says "an array of numbers", not "numbers, dates or ranges"; `tid[]` and `pg_lsn[]` added. The `NpgsqlPath?[]` pattern is a one-line fix worth doing either way. Small effort, no risk; the user keeps the text workaround.
  3. Ruled out: `Array Nullability Mode=PerInstance` set by DbLive. DbLive does not change the connection string of the user, a data source of its own gets another session in the `TransactionScope` (0096-166), and `PerInstance` cannot read `timestamp[]` at all (above).
  - Recommendation: 1. The value is plain data that a test of a report function returns naturally, and the workaround makes the user write the array literal of PostgreSQL. The change touches only values that fail today.
- Decision of 2026-11-24 (the maintainer): approach 1; the `NpgsqlPath?[]` fix and the docs of `tid[]`, `pg_lsn[]` and `PerInstance` in this task.
- Found while doing it (verified on 2026-11-24, Npgsql 10.0.1): through DbLive, `Array Nullability Mode=PerInstance` fails an array of `timestamp` already in `GetColumnSchemaAsync`, before the values, with `InvalidOperationException: A CLR type 'System.Object' was passed but the resolved PgTypeInfo does not have an equal Type: System.Array.` (plain `GetValue` fails with `Reading as 'System.Object' is not supported ...`). So the error names no column and is not the one of 0096-166. Left to the docs: a setting of the user that DbLive no longer needs, with its own row in §17.
- A user-defined range type (`create type floatrange as range (subtype = float8)`) is outside the map: its array with a NULL element keeps the error (verified by the test).

## Result
- `PostgreSqlDA.GetValue`: when `GetValue` fails with `InvalidCastException` on an array whose element (`PostgresArrayType.Element.InternalName`) is in `PostgreSqlDA.NullableArrayReaders`, the value is read as `GetFieldValue<T?[]>`; when that fails too (a multidimensional array), the error of `GetValue` stays. The map has the 36 built-in element types that Npgsql reads as a .NET value type, each as the type of `GetValue` for its array without NULL. A value that was read before is read the same way.
- `PostgreSqlDA.ReadValue` writes a `NpgsqlPath?[]` as text, as a `NpgsqlPath[]` (0096-154): a `path[]` with a NULL element, and every `path[]` with `Array Nullability Mode=Always` or `PerInstance` in the connection string of the user.
- Decisions of 2026-11-24 (the maintainer): approach 1 of Notes, the `path` fix and the docs in this task. Not breaking: a test that failed can pass, a test that passed compares the same texts, but for an array of `path` with `Array Nullability Mode`, which compared nothing before (as decided for 0096-154). No `MinCompatibleVersion`: the `dblive` tables do not change.
- Tests (`DbLive.PostgreSQL.Tests/SqlTestResultSetTests`):
  - `Rows_test_compares_arrays_with_a_NULL_element`: arrays of 27 element types with a NULL element compared by `rows-with-schema`, also a NULL first, only NULL, other bounds; `array_agg` of a nullable column equals `array[10, null, 30]`; an array that differs in a NULL position, the scale of a `numeric`, a `date`, a range fails, `[10,20,30]` against `[10,null,30]` with the message; an open `path` against a closed one in an array with NULL fails with `["[(0,0),(1,1)]",null]`. Without the fix it fails at the first value.
  - `Rows_test_compares_arrays_with_the_array_nullability_mode_of_the_connection_string`: with `Always` and `PerInstance` arrays with and without NULL pass, an open `path` against a closed one in an array fails (without the fix it passed with `Always`); with `PerInstance` an array of `timestamp` fails with the error of Npgsql that the docs quote.
  - `Nullable_array_readers_read_the_element_types_of_GetValue`: for every element of the map, the name is the one of Npgsql, the map gives the element type of `GetValue` and the same text of `rows`, and reads an array with NULL.
  - `Test_that_cannot_read_a_value_names_the_column_and_says_to_select_it_as_text`: a two-dimensional array with NULL, an array with NULL of a user-defined range, `tid[]` and `pg_lsn[]` keep the error of 0096-166.
  - All 109 tests of `DbLive.PostgreSQL.Tests` pass.
- Docs: `docs/llms-full.txt` §8.3 (arrays with NULL, the values that cannot be read, `PerInstance`), §17 (the row of the read error, a row for the `PerInstance` error); `docs/unit-testing/index.md` ("How values are compared").
