---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# PostgreSQL SQL tests: an `interval` with months, and a `void` function called with `select`

## Release note
The docs cover two cases of PostgreSQL SQL tests. An `interval` with months or years cannot be read: the test fails with `Cannot read interval values with non-zero months as TimeSpan ...`, whatever it asserts; cast the value to `text` in the test. A function called with `select` in the Arrange part returns a result set, also a `void` one: `rows`, `row-count=N`, `empty` and `single-row` fail on it, and `has-rows` checks its row and always passes. Call such a function with `perform` in a `do` block: `do $$ begin perform add_user('x'); end $$;`. DbLive behaves as before; nothing to change on upgrade.

## Problem
Two cases of PostgreSQL tests that the docs do not cover. Both verified by the review on 2026-11-24 (Npgsql 10.0.1, `postgres:18`).

1. **An `interval` with months cannot be read.** `select interval '1 month'` fails the test with `InvalidCastException: Cannot read interval values with non-zero months as TimeSpan ...`. DbLive reads the values of every result set, so it fails with any assert and without one. `docs/llms-full.txt` §8.3 (line 557) and §17 (line 1122) document the same case for a `numeric` `NaN`, with the fix.
2. **`select f()` of a `void` function returns a result set.** `select add_user('x');` in the Arrange part returns one row with a column `add_user` and `NULL`. It is an actual result set (§8.3, line 562), so `rows` fails with `Expected 1 result set(s), but 2 received.`, and `row-count=N`, `empty`, `single-row` with `Only one result set is expected, but multiple received.` On PostgreSQL calling a function in the Arrange part is the usual way; §8.3 gives only SQL Server examples (`update ... output`, a `select` for debugging).

## Docs to update
- `docs/llms-full.txt` §8.3: (1) next to `numeric` `NaN`: cast an `interval` to `text`, or `extract(epoch from ...)`; (2) a `void` function in the Arrange part: `perform` in a `do` block (`do $$ begin perform add_user('x'); end $$;`), or `setup.test.sql`
- `docs/llms-full.txt` §17: the `InvalidCastException` of an `interval` in the row of `numeric` `NaN`
- `docs/unit-testing/index.md` (the same two points)

## Done when
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- Verified again on 2026-11-24 with a temporary test in `src/DbLive.PostgreSQL.Tests` (Npgsql 10.0.1, `postgres:18`), removed afterwards:
  - `select interval '1 month' as i;` without an assert fails: `TestRunResult.ErrorMessage` is the message of Npgsql, `Cannot read interval values with non-zero months as TimeSpan, since that type doesn't support months. Consider using NodaTime Period ...`, and `Exception` a `DbLiveSqlException` with the `InvalidCastException` inside.
  - `interval '1 day 02:00'` equals `interval '26 hours'` in `rows`: both are the same `TimeSpan`.
  - `select add_user('x');` before the act: `rows` fails with `Expected 1 result set(s), but 2 received.`, `single-row` with `Only one result set is expected, but multiple received.`
  - `has-rows` passes after `select add_user('x');` when the act returns no rows: it checks the first actual result set, the row of the function. Not in the task; added to the docs.
  - `do $$ begin perform add_user('x'); end $$;` returns no result set, and the `rows` test passes.
- §17: a row of its own for the `interval` error instead of a longer row of `numeric` `NaN`: the cause and the fix differ (only `text` fits an `interval`).

## Result
Docs only, no change in behavior.
- `docs/llms-full.txt` §8.3: after `numeric` `NaN`, an `interval` with months or years cannot be read (the message, with any assert and without one); cast it to `text`, or select `extract(epoch from ...)`; an `interval` of days and less is compared as a `TimeSpan`. After "Every result set returned before the assert result set is actual": on PostgreSQL a function called with `select` in the Arrange part, also a `void` one, returns a result set; the errors of `rows`, `row-count=N`, `empty`, `single-row`, and that `has-rows` always passes; call it with `perform` in a `do` block, or in `setup.test.sql`.
- `docs/llms-full.txt` §17: a row for `Cannot read interval values with non-zero months as TimeSpan ...`.
- `docs/unit-testing/index.md`, "How values are compared": the `interval` point; the note "`rows` compares all actual output": the `void` function and `perform`.
