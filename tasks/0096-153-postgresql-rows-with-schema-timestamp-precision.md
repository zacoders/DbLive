---
type: bug
priority: low
created: 2026-11-24
---
# `rows-with-schema` on PostgreSQL ignores the precision of `timestamp`, `timestamptz` and `time`

## Problem
`PostgreSqlDA.GetPostgresTypeName` names `timestamp(N)`, `timestamptz(N)` and `time(N)` from `NumericScale`. Npgsql 10 gives the precision of these types in `NumericPrecision`, and `NumericScale` is null. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18` (`GetColumnSchema()` with `CommandBehavior.KeyInfo`): `timestamptz(3)` has `NumericPrecision = 3`, `NumericScale` null; `time(2)` has `NumericPrecision = 2`. So these branches never add the precision: a column `timestamp(0)` passes `rows-with-schema` against an expected `timestamp`, `time(2)` against `time`. On SQL Server `datetime2(3)` against `datetime2(7)` and `time(3)` against `time(7)` fail (`Rows_with_schema_test_fails_for_time_of_another_scale`).

`docs/llms-full.txt` §8.3 (line 542) names precisions for SQL Server only, so no line of the docs is wrong; the intent of the code is.

Cosmetic, same method: an unbounded `varbit` is named `varbit()`, an unbounded `bpchar` `char(1)` (from the code, not run).

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:485-496` (`timestamp`, `timestamptz`, `time`), `:479` (`bpchar`), `:498` (`varbit`)

## Fix
Take the precision of the three types from `NumericPrecision`. Check what Npgsql gives for a column without a precision (`timestamp` is `timestamp(6)` on the server): the type text must stay `timestamp` for it, or the expected value `cast(... as timestamp)` must give the same text. Name an unbounded `varbit` `varbit`.

- [ ] `timetz` and `interval` (with a precision or fields) the same way?

## Docs to update
- `docs/llms-full.txt` §8.3 (`rows-with-schema`): the PostgreSQL types with a precision, if the text changes

## Done when
- [ ] Fixed, with tests: `timestamp(0)` against `timestamp`, `time(2)` against `time` fail; equal types pass
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
