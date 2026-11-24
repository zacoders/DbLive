---
type: docs
priority: low
created: 2026-11-24
---
# PostgreSQL SQL tests: an `interval` with months, and a `void` function called with `select`

## Problem
Two cases of PostgreSQL tests that the docs do not cover. Both verified by the review on 2026-11-24 (Npgsql 10.0.1, `postgres:18`).

1. **An `interval` with months cannot be read.** `select interval '1 month'` fails the test with `InvalidCastException: Cannot read interval values with non-zero months as TimeSpan ...`. DbLive reads the values of every result set, so it fails with any assert and without one. `docs/llms-full.txt` §8.3 (line 557) and §17 (line 1122) document the same case for a `numeric` `NaN`, with the fix.
2. **`select f()` of a `void` function returns a result set.** `select add_user('x');` in the Arrange part returns one row with a column `add_user` and `NULL`. It is an actual result set (§8.3, line 562), so `rows` fails with `Expected 1 result set(s), but 2 received.`, and `row-count=N`, `empty`, `single-row` with `Only one result set is expected, but multiple received.` On PostgreSQL calling a function in the Arrange part is the usual way; §8.3 gives only SQL Server examples (`update ... output`, a `select` for debugging).

## Docs to update
- `docs/llms-full.txt` §8.3: (1) next to `numeric` `NaN`: cast an `interval` to `text`, or `extract(epoch from ...)`; (2) a `void` function in the Arrange part: `perform` in a `do` block (`do $$ begin perform add_user('x'); end $$;`), or `setup.test.sql`
- `docs/llms-full.txt` §17: the `InvalidCastException` of an `interval` in the row of `numeric` `NaN`
- `docs/unit-testing/index.md` (the same two points)

## Done when
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
