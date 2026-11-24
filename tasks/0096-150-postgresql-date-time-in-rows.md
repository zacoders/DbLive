---
type: docs
priority: medium
created: 2026-11-24
---
# `rows` on PostgreSQL: a `date` is `"2026-01-01"`, not `"2026-01-01T00:00:00"`

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
- [ ] Docs updated
- [ ] A test of the PostgreSQL value comparison that pins the text of `date` and `time` (a Npgsql upgrade that changes it then fails a test)

## Notes
- Found by the code review of 2026-11-24.
