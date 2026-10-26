---
type: bug
priority: medium
created: 2026-10-26
closed: 2026-10-26
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
- [x] Both tests fixed and passing
- [x] Docs updated, if the log hint is added

## Notes
Making `assert` mandatory was considered and rejected: tests that check with `throw` and smoke tests (`t.select-from-new-tables.sql`, `root.test.sql`) return no assert by design, so every such test would need a dummy assert, and existing user tests would break on upgrade.

Verified by an experiment before the fix: `asstert` and `Assert` (the column name was compared case-sensitively) both passed with no assertion.

## Result
Demo tests fixed as planned; both pass against SQL Server in Docker.

Instead of the log hint, the typo now fails the test, so the same mistake cannot pass silently in user projects:
- The `assert` column name is matched case-insensitively; assert values too, with spaces around them and around `=` ignored (`AssertTypeExtensions.TryParseAssertInfo`).
- With no assert result set, a result set whose first column is unnamed or within two edits of `assert`, and whose first value is a valid assert value, fails the test with `... looks like an assert result set, but its first column is named 'asstert' instead of 'assert' ...`.
- An assert result set without rows fails with a clear message instead of an index error. A bare `row-count` (no number) now gives the "use row-count=[Number]" message instead of `Expected  row(s)`. `row-count=N` with no actual result set reports N instead of a hard-coded 0.

Tests: `UnitTestResultCheckerAssertColumnTests`, `UnitTestResultCheckerRowCountTests`. Docs: `docs/llms-full.txt` §0 rule 8, §8.3, §17; `docs/unit-testing/index.md`.
