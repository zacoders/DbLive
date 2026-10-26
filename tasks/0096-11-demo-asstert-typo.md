---
type: bug
priority: medium
created: 2026-10-26
---
# Typo `asstert` in demo tests: two tests check nothing

## Problem
The column is named `asstert`, so there is no assertion, and the tests pass only because nothing throws. Once the typo is fixed, `select-order.test.sql` will fail: `select count(*)` returns one row, while `row-count=3` is expected.

## Where
- `src/Demo/DemoMSSQL/Scripts/Tests/Orders/select-order.test.sql:10`
- `src/Demo/DemoMSSQL/Scripts/Tests/Orders/update-order.test.sql:14`

## Fix
`asstert` → `assert`; in `select-order.test.sql` replace `select count(*)` with `select OrderId from dbo.Orders`. Optionally, have DbLive log a hint when a test without an assert returns result sets.

## Docs to update
The `count(*)` example in `README.md` is already fixed. If the log hint is added, describe it in `docs/llms-full.txt` §8.3 and `docs/unit-testing/index.md`.

## Done when
- [ ] Both tests fixed and passing
- [ ] Docs updated, if the log hint is added
