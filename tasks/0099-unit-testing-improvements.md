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

## Done when
- [ ] Every task above closed, or its decision not to do it recorded in its Notes

## Notes
- 2026-11-03: the testing tasks found during #96 (outside review of 2026-10-31) moved here: 0096-29 → 0099-01, 0096-23 → 0099-02, 0096-24 → 0099-03, 0096-28 → 0099-04.
- 2026-11-08: the comparison page moved here too, 0096-21 → 0099-05 (decision of the maintainer: the page is mostly about tests). Before, it stayed under #96 as documentation.
