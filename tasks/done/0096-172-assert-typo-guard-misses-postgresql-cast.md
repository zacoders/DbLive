---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# The typo guard of the assert column misses an unaliased cast on PostgreSQL

## Release note
None.

## Problem
The docs promise that a missing alias of the assert column fails the test:
- `docs/llms-full.txt` §0, rule 8 (line 20): "A misspelled name (`asstert`) or a missing alias (`select 'rows'`) → the test fails with "... looks like an assert result set ..."".
- `docs/unit-testing/index.md:38`: "A misspelled column (`select asstert = 'rows'`) or a missing alias (`select 'rows'`) does not silently turn the assertion off".

§8.3 (line 538) is exact: only an empty name (SQL Server) and `?column?` (PostgreSQL) count as no name.

PostgreSQL names an unaliased cast after its type. `select 'rows'::text;` returns a column named `text`, `select cast('rows' as varchar);` a column named `varchar`. Neither is unnamed nor within two edits of `assert`, so the test has no assertion and passes. Verified by the review of 2026-11-24 on `postgres:18`. `select 'rows';` (`?column?`) is caught; on SQL Server `select cast('rows' as varchar(10))` has an empty name and is caught. By the same rule of PostgreSQL `'rows'::char(4)` gives `bpchar` and `'rows'::name` gives `name` (verified later, see Notes).

It takes a mistake of the test author (a missing alias), and the guard exists for that mistake; the problem is that the docs promise more than it does.

## Where
`src/DbLive/Deployers/Testing/UnitTestResultChecker.cs:143`: `bool unnamed = columnName is "" or "?column?";`.

## Fix
- [x] Which one? (a) Narrow the docs: rule 8 and `index.md` say "a missing alias of a string literal (`select 'rows'`)", and §8.3 adds that on PostgreSQL a cast without an alias (`'rows'::text`) is named after its type and is not caught. (b) Extend the guard: on PostgreSQL a first column named `text`, `varchar`, `bpchar` or `name` counts as unnamed too. (b) also trips on an act whose first column is an unaliased cast to text with a value such as `rows` or `empty`, the case §8.3 already describes for an unnamed column.
  - (a). Decision of the maintainer, 2026-11-24: it is a mistake of the test author, the guard is not extended.

## Docs to update
- `docs/llms-full.txt` §0 rule 8, §8.3 (typo guard)
- `docs/unit-testing/index.md` (Anatomy, line 38)

## Done when
- [x] Decided, and docs (and the guard, for (b), with a unit test of `UnitTestResultChecker`) updated

## Notes
- Found by the code review of 2026-11-24 (the second one of that day).
- The reproduction of the review (`'rows'::text`, `cast('rows' as varchar)`) still holds: the guard (`UnitTestResultChecker.cs:135-152`) has not changed since; `f20e886` (17:14 that day, before the review) changed only the type of its exception.
- Verified 2026-11-24, `psql` on `postgres:18` (18.6): the names of unaliased expressions. `'rows'::char(4)` → `bpchar`, `'rows'::name` → `name`, `text 'rows'` → `text`, `varchar 'rows'` and `'rows'::character varying(10)` → `varchar`, `'rows'::unknown` → `unknown`, a domain `d172` (`'rows'::d172`) → `d172`, `'rows'::citext` → `citext` (domain and extension created in a rolled-back transaction), `lower('ROWS')` → `lower`, `format('row-count=%s', 3)` → `format`, `concat(...)` → `concat`, `coalesce(null, 'rows')` → `coalesce`, `case when true then 'rows' end` → `case`; `'row-' || 'count=3'` and `(select 'rows')` → `?column?` (caught). So the gap is any unaliased expression but a bare literal, an operator or a scalar subquery: casts, function calls, `case`, `coalesce`. That the test then passes without an assertion follows from the code (the guard looks only at the name; Npgsql reads `name` and `bpchar` as `string`, and the parser trims the padding of `char(n)`); not run through DbLive.
- `0096-11` (the guard itself, `breaking: true`) closed after `1.5.0` and is not released yet: a change of the guard here is part of the same release 2.0, and its effect on users belongs to the release note of `0096-11`.
- (b) was weighed against (a): it would close only the casts to string types (functions, `case`, `coalesce`, domains and `citext` stay open, so the docs need narrowing anyway), and each added name is a new false positive for an act column without an assert; the checker does not know the engine, so a SQL Server column named `name` or `text` would trip it too.

## Result
Docs only, the guard is unchanged.
- `docs/llms-full.txt` §0 rule 8: "a missing alias of a literal"; on PostgreSQL a cast or a function call without an alias is not caught, always write `as assert`.
- `docs/llms-full.txt` §8.3, typo guard: the guard knows only `''` and `?column?`; which expressions PostgreSQL names `?column?` and which it names after a type, a function or a keyword (verified on PostgreSQL 18); such a column is not caught.
- `docs/unit-testing/index.md` (Anatomy): the same as rule 8.

The release note of `0096-11` stays correct: it names "a first column unnamed or named close to `assert`", which is what the guard does.

Tests: none, no code changed.
