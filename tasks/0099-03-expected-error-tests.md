---
type: docs
priority: medium
created: 2026-10-31
---
# Testing that a procedure raises an error

## Problem
tSQLt has `ExpectException`. In DbLive an error in the act fails the test, and the docs show no way to test that a procedure rejects bad input. Validation errors are part of a procedure's contract, so this is a common test.

Not verified yet:
- SQL Server: `begin try exec ...; throw 50000, 'Expected an error.', 0; end try begin catch ... end catch` inside the test transaction. An error inside `try` can doom the transaction (`xact_state() = -1`), after which the test cannot run more statements. DbLive does not set `XACT_ABORT` (no match in `src` except a demo undo script); a procedure may set it itself. Does the runner report a doomed transaction clearly?
- PostgreSQL: a `do $$ ... exception when ... $$` block. The block must not catch its own "expected an error" exception. Does the act still work when it is a `call`?

## Plan
- [ ] Find working patterns on both engines and add them as demo tests (`src/Demo/DemoMSSQL`, `src/Demo/Demo.PostgreSQL.Chinook`), including a check of the error message.
- [ ] Document them.
- [ ] Decide whether an assert is worth adding, for example `select assert = 'error'` with the expected message after it. Problem: the act throws, so the assert result set would come before the act, and an error in the arrange must not pass the test. If yes, make it a separate feature task.

## Docs to update
- `docs/llms-full.txt` §8.3 (custom checks), §8.4 (example)
- `docs/unit-testing/index.md` ("Custom checks")
- Comparison page (0096-21): `ExpectException` row

## Done when
- [ ] Patterns verified on SQL Server and PostgreSQL with demo tests
- [ ] Docs updated
- [ ] Decision on a built-in assert recorded in Notes

## Notes
- 2026-11-03: moved from #96 (0096-24) to #99.
