---
type: bug
priority: medium
created: 2026-10-26
closed: 2026-10-26
---
# `rows` check is incomplete

## Problem
Only the expected rows are compared. Extra rows and extra result sets in the actual output go unnoticed. When there are fewer actual rows or result sets than expected, the test fails with `ArgumentOutOfRangeException` instead of a clear message.

## Where
`src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:16` (loop over expected result sets) and `:142` (loop over expected rows).

## Fix
Compare the number of result sets and rows first, with a message like "Expected 3 rows, but 5 received". This changes behavior: tests that passed on a partial match will start failing. Mention it in the release notes.

## Docs to update
- `docs/llms-full.txt` §8.3 (Limitations item), §19
- `docs/unit-testing/index.md` (note "Limitations of `rows`")
- Recheck the examples in `README.md`

## Done when
- [x] Row and result set counts checked, with unit tests for more, fewer and equal
- [x] Docs updated
- [x] Behavior change noted for the release

## Notes
Verified by an experiment before the fix: extra actual rows, extra actual result sets and a `rows` assert with no expected result set all passed.

## Result
`UnitTestResultChecker.CompareResultSets` checks, in order: at least one expected result set, equal result set counts, then per result set the columns, the row count and the values. Messages: `No expected result set after the 'assert' result set.`, `Expected N result set(s), but M received.`, `Expected N row(s), but M received.`; with several expected result sets the message is prefixed with `Result set N:`. Tests: `UnitTestResultCheckerRowsTests`. The demo tests (SQL Server, PostgreSQL Chinook, AdventureWorks) pass with the stricter check.

Docs: `docs/llms-full.txt` §8.3, §17, §19; `docs/unit-testing/index.md` (the note is now "`rows` compares all actual output"); `README.md` assertions table. The README examples need no change.

Release note (there is no release notes file in the repo yet, copy this into the release):

> **Breaking: `rows` and `rows-with-schema` now compare the whole actual output.** A test fails when the actual results have more or fewer result sets or rows than expected (before, only the expected rows were compared, and extra output passed unnoticed). Output from the Arrange part, such as `update ... output`, now counts as an actual result set: remove it or move the setup into `init.sql`.
