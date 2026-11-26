---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
breaking: true
---
# `rows-with-schema` on PostgreSQL ignores the precision of `timestamp`, `timestamptz` and `time`

## Release note
**`rows-with-schema` compares the precision of PostgreSQL date and time types.** `timestamp`, `timestamptz`, `time`, `timetz` and `interval` columns are compared with their precision: `timestamp(0)` is not `timestamp`, `time(2)` is not `time`. Before, the precision was ignored. Precision 6 is the same type as no precision (`timestamp(6)` equals `timestamp`), and the fields of an `interval` (`interval year`) are not compared. An unbounded `bpchar` no longer equals `char(1)`, and the failure message names an unbounded `varbit` `varbit` (was `varbit()`) and `timetz` `timetz` (was `time with time zone`). A `rows-with-schema` test whose expected value has another precision than the actual column now fails: cast the expected value to the type of the column. PostgreSQL returns the columns of a function without their precision, so cast the expected values of a function without it: `cast('2026-01-01' as timestamp)`, not `timestamp(0)`.

## Problem
`PostgreSqlDA.GetPostgresTypeName` names `timestamp(N)`, `timestamptz(N)` and `time(N)` from `NumericScale`. Npgsql 10 gives the precision of these types in `NumericPrecision`, and `NumericScale` is null. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18` (`GetColumnSchema()` with `CommandBehavior.KeyInfo`): `timestamptz(3)` has `NumericPrecision = 3`, `NumericScale` null; `time(2)` has `NumericPrecision = 2`. So these branches never add the precision: a column `timestamp(0)` passes `rows-with-schema` against an expected `timestamp`, `time(2)` against `time`. On SQL Server `datetime2(3)` against `datetime2(7)` and `time(3)` against `time(7)` fail (`Rows_with_schema_test_fails_for_time_of_another_scale`).

`docs/llms-full.txt` §8.3 (line 542) names precisions for SQL Server only, so no line of the docs is wrong; the intent of the code is.

Cosmetic, same method: an unbounded `varbit` is named `varbit()`, an unbounded `bpchar` `char(1)` (from the code, not run).

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:491-501` (`timestamp`, `timestamptz`, `time`), `:485` (`bpchar`), `:504` (`varbit`)

## Fix
Take the precision of the three types from `NumericPrecision`. Check what Npgsql gives for a column without a precision (`timestamp` is `timestamp(6)` on the server): the type text must stay `timestamp` for it, or the expected value `cast(... as timestamp)` must give the same text. Name an unbounded `varbit` `varbit`.

- [x] `timetz` and `interval` (with a precision or fields) the same way? Yes for the precision: Npgsql gives it in `NumericPrecision` for both (verified). The fields of an `interval` it does not give: `interval year`, `interval hour to minute` and `interval day to second` all have `NumericPrecision = 65535`, and `NpgsqlDbColumn` has no type modifier (verified, see Notes).

## Docs to update
- `docs/llms-full.txt` §8.3 (`rows-with-schema`): the PostgreSQL types with a precision, if the text changes
- `docs/unit-testing/index.md` ("Assertions")

## Done when
- [x] Fixed, with tests: `timestamp(0)` against `timestamp`, `time(2)` against `time` fail; equal types pass
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- What Npgsql 10.0.1 gives on `postgres:18`, the same for a table column and for `cast(... as ...)` (verified 2026-11-24, a probe test, not kept). `DataTypeName` has no precision: `timestamp without time zone`, `time with time zone`, `interval`, `bit varying`, `character`.
  - `NumericPrecision`: `timestamp`, `timestamptz`, `time`, `timetz`, `interval` without a precision: null; `(0)`..`(6)`: 0..6; `interval` with fields and without a precision: 65535; `interval day to second(2)`: 2. `NumericScale` is null for all of them.
  - `ColumnSize`: `varbit` and `bpchar` without a length: null; `char`: 1; `bit`: 1.
  - A function `returns table (a timestamp(0), b time(2), c varchar(10), d numeric(10,2))` returns its columns without the type modifier: precision null, so `timestamp`, `time`, `text`, `numeric`. PostgreSQL drops the modifiers of a function result.
  - `reader.GetDataTypeName(i)` builds the name with the precision (`timestamp(3) without time zone`, `numeric(10, 2)`, `character varying(10)[]`, `interval(65535)`, a domain as its base type). Not used: DbLive would parse a text whose format is Npgsql's, and the fields of an `interval` are lost there too.
  - `format_type(oid, typmod)` on the server would give the exact name with the fields of an `interval`, but Npgsql does not expose the type modifier of a column (`NpgsqlDbColumn` has no `TypeModifier`; `pg_attribute` has it for table columns only, not for the expected `cast` side). Not used.
- Before the fix, `rows-with-schema` passed (verified): `timestamp(0)` / `timestamptz(3)` / `time(2)` / `timetz(2)` / `interval day to second(2)` against the type without a precision, `time(2)` against `time(0)`, a table column `timestamp(0)` against `cast(... as timestamp)`, `bpchar` against `char(1)`. `varchar(10)` against `varchar(20)` and `numeric(10,2)` against `numeric(10,3)` failed, as they should.
- Decision of 2026-11-24 (recommended with the fix, which the maintainer asked for): precision 6 is named without it. A type without a precision keeps microseconds, as precision 6 does, so `timestamp(6)` and `timestamp` hold the same values; the same as `datetime2` and `datetime2(7)` on SQL Server, and as `varchar` and `text`, `numeric(10,0)` and `numeric(10)` that `rows-with-schema` already takes as one type.
- Arrays are not changed: Npgsql names them by the long name without the modifier (`timestamp without time zone[]`, `character varying[]`), so `timestamp(3)[]` equals `timestamp[]`.
- Found while checking: a test that selects a column of an enum or a domain created by a migration in the same process fails with `KeyNotFoundException` from `GetColumnSchemaAsync`: 0096-165.

## Result
- `GetPostgresTypeName` takes the precision of `timestamp`, `timestamptz`, `time`, `timetz` and `interval` from `NumericPrecision` (`WithSecondsPrecision`): `timestamp(0)`..`timestamp(5)`; precision 6, none and 65535 (an `interval` with fields) give the name alone. `time with time zone` is named `timetz`. An unbounded `varbit` is `varbit`, an unbounded `bpchar` is `bpchar`.
- `breaking: true`: a `rows-with-schema` test that compared a column with an expected value of another precision passed and now fails, also a test of a function with an expected `cast(... as timestamp(0))`.
- Tests (`DbLive.PostgreSQL.Tests/SqlTestResultSetTests.cs`): `Rows_with_schema_test_compares_the_precision_of_date_and_time_types` (a table column `timestamp(0)`, `time(2)` against the types without a precision fails with the message; against the same precision passes; a function against the types without a precision passes; `timestamptz(3)`, `timetz(2)`, `interval(3)` fail; precision 6 against none passes, `interval hour to minute` against `interval` too), `Rows_with_schema_test_names_unbounded_bpchar_and_varbit`.
- Docs: `docs/llms-full.txt` §8.3 (`rows-with-schema`: the PostgreSQL types with a precision, precision 6, the fields of an `interval`, the columns of a function), `docs/unit-testing/index.md` ("Assertions").
