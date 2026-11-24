---
type: feature
priority: medium
created: 2026-11-03
---
# Unit testing improvements

## Goal
Rich tools for unit tests of database objects: the helpers `dblive.FakeTable`, `dblive.SpyProcedure` and `dblive.FakeFunction`, which let a test isolate the object under test from its dependencies, and other improvements of SQL tests.

Tests are now written mostly by LLMs. Prefer APIs they already know: the helpers take the names, parameters and behavior of tSQLt (decision of the maintainer, 2026-11-03, in 0099-01).

## Tasks
- [ ] `0099-01-test-helpers.md`: `dblive.FakeTable`, `dblive.SpyProcedure`, `dblive.FakeFunction`
- [ ] `0099-02-rows-unordered-assert.md`: an assertion that compares rows in any order
- [ ] `0099-03-expected-error-tests.md`: testing that a procedure raises an error
- [ ] `0099-04-several-assertions-per-test.md`: several assertions in one test
- [ ] `0099-05-comparison-and-tsqlt-page.md`: comparison page and "Coming from tSQLt"; its tSQLt rows follow the tasks above

Ideas about tests, not decided yet:
- `tasks/ideas/migration-step-tests.md`: tests of a migration step on prepared data
- `tasks/ideas/migration-breaking-change-check.md`: a heuristic check that a release does not break the previous application (schema compare)

`tasks/rejected/undo-test-data-round-trip.md` (data checks through undo) was rejected on 2026-11-06 with undo scripts (0096-46).

## Mandatory assert
Every test states its check: a test without an assert result set fails. A test that checks with `throw`, or only that its script runs, says so with an assert value of its own, for example `select 'none' as assert`. A test written wrong then fails at once with a clear message, instead of passing without a check.

Decision of the maintainer, 2026-11-24: do it, in a release after 2.0.0, which has many changes already. This withdraws the rejection in `tasks/done/0096-11-demo-asstert-typo.md` (Notes, 2026-10-26), which may have been taken by accident.

Today a test without an assert result set passes if its script runs without an error (`docs/llms-full.txt` §8.3), so any mistake that loses the assert result set leaves a test that checks nothing:
- An assert column that the typo guard does not catch: on PostgreSQL a cast, a function call, `case` or `coalesce` without an alias (`select 'rows'::text` returns a column `text`). 0096-172 narrowed the docs and left the gap.
- An empty test file, a placeholder for a test to write later: passes on both providers (0096-171).
- The guard is a heuristic with a false positive: a test without an assert whose act returns an unnamed string column with a value such as `rows` fails. With a mandatory assert the guard is only a hint in the message of a test without an assert, and the false positive goes away.
- Tests are written mostly by LLMs (Goal): one rule with a clear error is easier to follow than "an assert is optional, but a column that looks like one is an error".

0096-11 rejected it because tests that check with `throw` and smoke tests return no assert by design, so each would need a dummy assert, and existing tests would break on upgrade. Since then:
- The guard that 0096-11 chose instead has known gaps on PostgreSQL (0096-172), and an empty test passes (0096-171).
- The assert of a `throw` test is not a dummy: it says that the test checks with `throw`, which a reader and the runner now have to guess.
- The upgrade stays a cost, but a small one: the message of a failed test says what to add.

Sketch:
- A new assert value: the test passes when its script runs without an error. Result sets before it are ignored (a smoke test `select * from dbo.Orders`).
- No assert result set: the test fails with `The test has no assert result set. Add one, for example: select 'rows' as assert; a test that checks with throw: select 'none' as assert.` When a result set looks like a misnamed assert, the message names its column, as the guard does now.
- An empty test file fails with the same message.
- `setup.test.sql` is not a test and needs no assert.
- Breaking: every test without an assert fails after the upgrade, and the message says what to add. The release note describes the change from 2.0.0: a test without an assert and an empty test file fail instead of passing, and the typo guard is only a hint in that message.

Open questions:
- [x] Make the assert mandatory? Yes, after 2.0.0 (maintainer, 2026-11-24).
- [ ] The name of the value: `none`, `nothing`, `no-error`?
- [ ] A result set after this value: fail the test (it looks like an expected result set written by mistake)?
- [ ] It is breaking, so by the PR title rule (`BREAKING CHANGE:`, "Releases" in `Development.md`) the release that brings it is 3.0.0. Straight there, or first a 2.x release that only logs a warning for a test without an assert?

What it changes:
- 0099-03 (expected errors): the `throw` patterns end with the new value, or check the message with `rows` as the verified pattern in its Notes does. A built-in `error` assert, placed before the act, is proposed there (2026-11-24).
- 0099-01 (helpers) and 0099-04 (several assertions): no change of the design; their examples and demo tests have an assert.
- 0099-05 (comparison page): the assertions row says that every test states its check; check what tSQLt does with a test without an assertion in its docs.
- Tests without an assert in the repository: the template `Tests/user-name-is-required.sql` (SQL Server and PostgreSQL, its comment says "Without an assert, the test passes ..."), `src/Demo/DemoMSSQL` (`Code/root.test.sql`, `Tests/root.test.sql`, `Tests/Common/admin-must-exists.test.sql`, `Tests/Common/t.select-from-new-tables.sql`, `Tests/Common/unique-user-name.test.sql`), `src/DbLive.Tests/Scripts/Tests/example.sql`, integration tests such as `SqlTestResultSetTests.Empty_test_passes`.
- Docs: `docs/llms-full.txt` §0 rule 8, §8.3 (no assert, typo guard), the template description; `docs/unit-testing/index.md` (Anatomy, "Without an assert result set, raise an error"); `README.md` (assertions table, the "_(no assert)_" row).

## Done when
- [ ] Every task above closed, or its decision not to do it recorded in its Notes
- [x] Mandatory assert decided (maintainer, 2026-11-24: yes, after 2.0.0)
- [ ] Mandatory assert implemented with unit tests, the tests and docs listed under "Mandatory assert" updated

## Notes
- 2026-11-03: the testing tasks found during #96 (outside review of 2026-10-31) moved here: 0096-29 → 0099-01, 0096-23 → 0099-02, 0096-24 → 0099-03, 0096-28 → 0099-04.
- 2026-11-08: the comparison page moved here too, 0096-21 → 0099-05 (decision of the maintainer: the page is mostly about tests). Before, it stayed under #96 as documentation.
