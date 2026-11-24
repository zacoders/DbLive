---
type: bug
priority: low
created: 2026-11-24
---
# `rows`: different PostgreSQL `tsquery` values are equal, and an open `path` equals a closed one

## Problem
`RowValueText` writes a value as text only when its type has no public properties (or is `IPAddress`, a SQL Server CLR type); other types get the default JSON of System.Text.Json. For two Npgsql types that JSON does not show the value. Verified on 2026-11-24 by the review (Npgsql 10.0.1, `postgres:18`, a copy of `RowValueText`):

- `NpgsqlTsQueryAnd` writes its operands by their declared type `NpgsqlTsQuery`, which has only the property `Kind`: `'a & b'::tsquery` and `'a & c'::tsquery` both give `{"Left":{"Kind":0},"Right":{"Kind":0},"Kind":2}`.
- `NpgsqlPath` is written as an array of points, without its `Open` flag: `'((0,0),(1,1))'::path` and `'[(0,0),(1,1)]'::path` both give `[{"X":0,"Y":0},{"X":1,"Y":1}]`.

So a function that returns the wrong `tsquery` passes its `rows` test. `docs/llms-full.txt` §8.3 (lines 555-556) promises that a value is compared by a text that shows it, or the test fails with "Cannot compare ...". The same kind of bug as 0096-108 and 0096-145.

## Where
- `src/DbLive/Deployers/Testing/RowValueText.cs:63-88` (`TextConverterFactory.CanConvert`)

## Fix
Write `NpgsqlTsQuery` and `NpgsqlPath` by their `ToString()` (check that it is the text PostgreSQL shows). `RowValueText` is in `DbLive`, which does not reference Npgsql: match the types by name, or convert the value in `PostgreSqlDA.ReadValue`, as `inet` is (0096-145).

- [ ] Other Npgsql types whose JSON loses a part of the value (`tsvector` weights and positions, `NpgsqlPolygon`, `NpgsqlBox`, ranges)? Check them in the same test.

## Docs to update
- `docs/llms-full.txt` §8.3 (the list of values compared by their text)
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [ ] Fixed, with tests: two different `tsquery` values and an open against a closed `path` fail `rows`
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
