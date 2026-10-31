---
type: feature
priority: medium
created: 2026-10-31
---
# Assertion that compares rows in any order

## Problem
`rows` compares rows by position, so row order matters (`docs/llms-full.txt` §8.3). The docs say "use `order by` in the act", but when the act is `exec dbo.GetOrders`, the test cannot add `order by`: it has to capture the output first (`insert into #t exec ...; select * from #t order by ...`), which is noise in every such test. Procedures whose contract does not promise an order are common.

## Where
`src/DbLive/Deployers/Testing/UnitTestResultChecker.cs`, `AssertType.cs`, `AssertTypeExtensions.cs`

## Fix
New assert values that compare each result set as a multiset of rows: same columns, same row count, and every row matched once.
- [ ] Names: `rows-unordered` and `rows-with-schema-unordered`? Or `rows-any-order`?
- [ ] Failure message: list the expected rows not found and the actual rows left over (first N of each).
- [ ] Same value comparison as `rows` (JSON representation), same count checks first.

## Docs to update
- `docs/llms-full.txt` §8.3 (table and value comparison), §8.4 (example)
- `docs/unit-testing/index.md` ("Assertions", "How values are compared": "Row order matters")
- `README.md` (assertions table)

## Done when
- [ ] Implemented with unit tests (same rows in another order, duplicates, missing and extra rows)
- [ ] A demo test uses it on SQL Server and PostgreSQL
- [ ] Docs updated
