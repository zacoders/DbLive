---
type: bug
priority: low
created: 2026-11-24
---
# The typo guard of the assert column misses an unaliased cast on PostgreSQL

## Problem
The docs promise that a missing alias of the assert column fails the test:
- `docs/llms-full.txt` §0, rule 8 (line 20): "A misspelled name (`asstert`) or a missing alias (`select 'rows'`) → the test fails with "... looks like an assert result set ..."".
- `docs/unit-testing/index.md:38`: "A misspelled column (`select asstert = 'rows'`) or a missing alias (`select 'rows'`) does not silently turn the assertion off".

§8.3 (line 538) is exact: only an empty name (SQL Server) and `?column?` (PostgreSQL) count as no name.

PostgreSQL names an unaliased cast after its type. `select 'rows'::text;` returns a column named `text`, `select cast('rows' as varchar);` a column named `varchar`. Neither is unnamed nor within two edits of `assert`, so the test has no assertion and passes. Verified by the review of 2026-11-24 on `postgres:18`. `select 'rows';` (`?column?`) is caught; on SQL Server `select cast('rows' as varchar(10))` has an empty name and is caught. By the same rule of PostgreSQL `'rows'::char(4)` gives `bpchar` and `'rows'::name` gives `name` (not run).

It takes a mistake of the test author (a missing alias), and the guard exists for that mistake; the problem is that the docs promise more than it does.

## Where
`src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:143`: `bool unnamed = columnName is "" or "?column?";`.

## Fix
- [ ] Which one? (a) Narrow the docs: rule 8 and `index.md` say "a missing alias of a string literal (`select 'rows'`)", and §8.3 adds that on PostgreSQL a cast without an alias (`'rows'::text`) is named after its type and is not caught. (b) Extend the guard: on PostgreSQL a first column named `text`, `varchar`, `bpchar` or `name` counts as unnamed too. (b) also trips on an act whose first column is an unaliased cast to text with a value such as `rows` or `empty`, the case §8.3 already describes for an unnamed column.

## Docs to update
- `docs/llms-full.txt` §0 rule 8, §8.3 (typo guard)
- `docs/unit-testing/index.md` (Anatomy, line 38)

## Done when
- [ ] Decided, and docs (and the guard, for (b), with a unit test of `UnitTestResultChecker`) updated

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
