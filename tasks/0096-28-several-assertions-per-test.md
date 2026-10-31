---
type: feature
priority: low
created: 2026-10-31
---
# Several assertions in one test

## Problem
A test may have one assert result set; a second one fails the test (`docs/llms-full.txt` §8.3). To check both what a procedure returns and what it wrote to a table, a user writes two tests with the same arrange, or replaces one assertion with `throw`.

## Fix
Open questions first:
- [ ] Boundaries: in `[actual 1] assert 1 [expected 1] [actual 2] assert 2 [expected 2]` it is not clear where expected 1 ends and actual 2 begins. Options: an end marker (`select assert = 'end'`), a count in the assert (`rows:2`), or "expected takes as many result sets as actual".
- [ ] Failure message: name the failed assertion by its position.
- [ ] Is it worth the extra syntax, compared with two tests sharing `setup.test.sql`?

## Where
`src/DbLive/Deployers/Testing/UnitTestResultChecker.cs`

## Docs to update
- `docs/llms-full.txt` §8.3, §8.4
- `docs/unit-testing/index.md` ("Assertions")

## Done when
- [ ] Decision taken
- [ ] Implemented with unit tests, or the decision not to recorded in Notes
