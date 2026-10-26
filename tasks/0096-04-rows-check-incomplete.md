---
type: bug
priority: medium
created: 2026-10-26
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
- [ ] Row and result set counts checked, with unit tests for more, fewer and equal
- [ ] Docs updated
- [ ] Behavior change noted for the release
