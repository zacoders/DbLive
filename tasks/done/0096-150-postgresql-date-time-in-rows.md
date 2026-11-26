---
type: docs
priority: medium
created: 2026-11-24
closed: 2026-11-24
---
# `rows` on PostgreSQL: a `date` is `"2026-01-01"`, not `"2026-01-01T00:00:00"`

## Release note
The docs now give the text by which `rows` compares a PostgreSQL `date`: `"2026-01-01"`, not `"2026-01-01T00:00:00"` as on SQL Server. So on PostgreSQL a `date` equals the string `'2026-01-01'` and never a `timestamp` at midnight; a `time` is `"10:00:00"` on both providers. Casting the expected value to the type of the column works on both, as before. DbLive behaves as before; nothing to change on upgrade.

## Problem
`docs/llms-full.txt` §8.3 (line 553): "A date serializes as `"2026-01-01T00:00:00"`, so `'2026-01-01'` never equals it". True on SQL Server, where SqlClient reads `date` as `DateTime`. Npgsql 10 reads `date` as `DateOnly` and `time` as `TimeOnly`, and `RowValueText` writes them as `"2026-01-01"` and `"10:00:00"`. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18`: `reader.GetValue` of `cast('2026-01-01' as date)` is `DateOnly`, of `cast('10:00' as time(2))` `TimeOnly`, of a `timestamptz` `DateTime`.

So on PostgreSQL `select cast('2026-01-01' as date) as d; select 'rows' as assert; select '2026-01-01' as d;` passes (verified by the review with a copy of `RowValueText`), and a `date` never equals a `timestamp` at midnight. The advice of the docs, to cast the expected value to the type of the column, works on both providers.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:456-462` (`ReadValue`: `reader.GetValue`)
- `src/DbLive/Deployers/Testing/RowValueText.cs`

## Fix
Docs only: the text of a date per provider. On SQL Server `date` is `"2026-01-01T00:00:00"`, on PostgreSQL `"2026-01-01"`, which also equals the string `'2026-01-01'`; `time` is `"10:00:00"` on both. The advice to cast stays.

## Docs to update
- `docs/llms-full.txt` §8.3 ("Dates")
- `docs/unit-testing/index.md:84` ("How values are compared"), if it names the text

## Done when
- [x] Docs updated
- [x] A test of the PostgreSQL value comparison that pins the text of `date` and `time` (a Npgsql upgrade that changes it then fails a test)

## Notes
- Found by the code review of 2026-11-24.
- The failure message of `rows` prints the columns of the expected result set: `Columns: d: timestamp` for a `date` compared with a `timestamp` (verified by the new test).

## Result
- `docs/llms-full.txt` §8.3, "Dates": the text of a `date` per provider. SQL Server: `DateTime`, `"2026-01-01T00:00:00"`, never equal to `'2026-01-01'`. PostgreSQL: `DateOnly` and `TimeOnly`, a `date` is `"2026-01-01"`, equals the string `'2026-01-01'` and never a `timestamp` at midnight (verified). `time` is `"10:00:00"` on both. The advice to cast stays first.
- `docs/unit-testing/index.md`, "How values are compared": the text of a `date` on each provider, and that on PostgreSQL it never equals a `timestamp` at midnight.
- Test `SqlTestResultSetTests.Rows_test_writes_date_and_time_as_PostgreSQL_shows_them` (`src/DbLive.PostgreSQL.Tests`): a `date` and a `time(2)` pass `rows` against the strings `'2026-01-01'` and `'10:00:00'`, and a `date` fails against a `timestamp` at midnight with `Expected values: "2026-01-01T00:00:00"`, `Actual values: "2026-01-01"`. Passes with Npgsql 10.0.1 and `postgres:18`.
