---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# `rows`: different PostgreSQL `tsquery` and `tsvector` values are equal, and an open `path` equals a closed one

## Release note
**`rows` compares PostgreSQL `tsquery`, `tsvector` and `path` values by their text.** `rows` compares values by their JSON, which lost a part of these values: two `tsquery` values with an operator were equal when their operands differed (`'a & b'` and `'a & c'`), and so were any two `tsquery` values in an array; two `tsvector` values were equal when their positions or weights differed (`'a:1A'` and `'a:1B'`); an open `path` equaled the closed one with the same points. Now these values are compared by their text, also in a one-dimensional array: `"'a' & 'b'"`, `"'a':1A 'b':2"`, `"[(0,0),(1,1)]"`. A test that starts failing compared nothing before: fix its expected value.

## Problem
`RowValueText` writes a value as text only when its type has no public properties (or is `IPAddress`, a SQL Server CLR type); other types get the default JSON of System.Text.Json. For some Npgsql types that JSON does not show the value. Verified on 2026-11-24 by the review (Npgsql 10.0.1, `postgres:18`, a copy of `RowValueText`), and again on the same day through `PostgreSqlDA` (Notes):

- `NpgsqlTsQueryAnd` writes its operands by their declared type `NpgsqlTsQuery`, which has only the property `Kind`: `'a & b'::tsquery` and `'a & c'::tsquery` both give `{"Left":{"Kind":0},"Right":{"Kind":0},"Kind":2}`. The same for `|`, `!` and `<->`, and for the elements of a `tsquery[]`.
- `NpgsqlTsVector` is written as an array of `{"Text","Count"}`, without the positions and weights.
- `NpgsqlPath` is written as an array of points, without its `Open` flag: `'((0,0),(1,1))'::path` and `'[(0,0),(1,1)]'::path` both give `[{"X":0,"Y":0},{"X":1,"Y":1}]`.

So a function that returns the wrong `tsquery` passes its `rows` test. `docs/llms-full.txt` §8.3 promises that a value is compared by a text that shows it, or the test fails with "Cannot compare ...". The same kind of bug as 0096-108 and 0096-145.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`, `ReadValue`
- `src/DbLive/Deployers/Testing/RowValueText.cs` (`TextConverterFactory.CanConvert`): not changed

## Fix
Store `NpgsqlTsQuery`, `NpgsqlTsVector` and `NpgsqlPath`, and their one-dimensional arrays, as their `ToString()` in `PostgreSqlDA.ReadValue`, as `inet` is (0096-145).

- [x] Other Npgsql types whose JSON loses a part of the value (`tsvector` weights and positions, `NpgsqlPolygon`, `NpgsqlBox`, ranges)? Check them in the same test. Yes: `tsvector` loses its positions and weights, and a `tsquery[]` loses even a single lexeme. The others keep the value (Notes).

## Docs to update
- `docs/llms-full.txt` §8.3 (the list of values compared by their text)
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [x] Fixed, with tests: two different `tsquery` values, two `tsvector` values with other positions or weights, an open against a closed `path` fail `rows`, also in arrays
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- Verified on 2026-11-24 with Npgsql 10.0.1, `postgres:18`, net8.0: a temporary test in `DbLive.PostgreSQL.Tests` read pairs of different values with `IDbLiveDA.QuerySqlAsync` (so through `PostgreSqlDA.ReadValue`) and wrote them with `RowValueText.Write`, the text that `rows` compares. Equal texts for different values:
  - `tsquery`: `'a & b'` = `'a & c'` (`NpgsqlTsQueryAnd`), `'a | b'` = `'a | c'` (`NpgsqlTsQueryOr`), `'!a'` = `'!b'` (`NpgsqlTsQueryNot`), `'a <-> b'` = `'a <-> c'` (`NpgsqlTsQueryFollowedBy`; only `Distance` is kept). A single lexeme keeps its text, weights and prefix.
  - `tsquery[]`: `array['b'::tsquery]` = `array['c'::tsquery]`, both `[{"Kind":0}]`: the elements are written by their declared type `NpgsqlTsQuery`.
  - `tsvector`: `'a:1 b:2'` = `'a:1 b:3'` (positions), `'a:1A'` = `'a:1B'` (weights). The lexemes and the number of positions are kept.
  - `path`: open = closed, also in `path[]`.
- Keep the value (verified the same way): `polygon`, `box`, `lseg`, `line`, `circle`, `point`, `int4range`, `numrange`, `tsrange` (bounds, inclusiveness, infinity, `empty`), `int4multirange`, `bit(3)` and `varbit` (`BitArray` as an array of booleans), `tid`, `xid`, `xid8`, `money`, `timetz`, `jsonpath`, an anonymous record (`object[]`), `"char"`, `int2vector`, `oidvector`, `cidr` (`IPNetwork`), `interval`.
- Ruled out, not this task: a multidimensional array (`int[][]` as `Int32[,]`) fails the test with `Cannot compare ...: Serialization and deserialization of 'System.Int32[,]' instances is not supported.`, as §8.3 promises. `hstore` created by the test itself was not read (`DataTypeName '-.-'`): the case left out by 0096-165; a non-empty `hstore` is a `Dictionary` and keeps its value (0096-108).
- Found: `regclass`, `pg_snapshot` and `aclitem` cannot be read at all, and the docs do not say so: 0096-168. An array of a .NET value type with a NULL element (`array[1, null]::int[]`, also `path[]`) fails the read: 0096-169.
- `ToString()` against the text of PostgreSQL (`::text`), verified the same way, also with `CurrentCulture` `ru-RU` (no difference by culture):
  - `NpgsqlTsVector`: the same text in every case checked (weights `'a':1A,2B,3`, a quote `'it''s':1`, a space, empty, unsorted positions).
  - `NpgsqlPath`: the same, except the exponent: `1E+20` against `1e+20`.
  - `NpgsqlTsQueryLexeme`: the same (weights, `:*`, quotes, a backslash, a space).
  - `NpgsqlTsQuery` with operators puts the parentheses in other places: `'a' <-> 'b' & 'c'` for PostgreSQL's `'a' <-> ( 'b' & 'c' )`, `( 'a' <-> 'b' ) & 'c'` for `'a' <-> 'b' & 'c'`, `!( !'a' )` for `!!'a'`. By the outputs, Npgsql puts a nested `|` or `<->` in parentheses and never a nested `&`. Of 11 pairs of different trees (`&`, `|`, `<->`, `<2>`, `!` nested both ways), only `(a & b) & c` and `a & (b & c)` got the same `ToString()`; PostgreSQL prints the same text for them too (`'a' & 'b' & 'c'`), though its `=` says false. So `ToString()` tells the trees apart as well as `::text` does, but a failure message can show a text that PostgreSQL parses into another query.
- Approaches considered: (1) `ToString()` in `PostgreSqlDA.ReadValue`; (2) the same with an own writer of `tsquery` that puts the parentheses as PostgreSQL does; (3) a rule by type in `RowValueText` without Npgsql (a collection or an abstract type with its own `ToString()`), which would write the root of a `tsquery` as `{"Left":"'a'",...}` and change the text of `polygon`. Decided by the maintainer on 2026-11-24: approach 1, with `tsvector` and the arrays in this task.
- Not `breaking`, as decided for `inet` in 0096-145: only the tests that compared values differing in the lost part start to fail, and they compared nothing before.

## Result
- `PostgreSqlDA.ReadValue` stores a `NpgsqlTsQuery`, `NpgsqlTsVector` or `NpgsqlPath` as its `ToString()`, and an array of them as an array of strings, with `null` for a NULL element. A multidimensional array stays as Npgsql reads it. `RowValueText` is not changed.
- Tests: `SqlTestResultSetTests.Rows_test_compares_text_search_values_and_paths` (PostgreSQL): `same.sql` (`rows-with-schema`) has `tsquery` with nested operators, weights and `<2>`, `tsvector` with positions and weights, empty, open and closed `path`, NULLs of each, arrays with a NULL `tsquery` and `tsvector`; `other-tsquery.sql` fails with `Expected values: "'a' & 'c'"`, `Actual values: "'a' & 'b'"`; `open-against-closed-path.sql` with `"[(0,0),(1,1)]"` against `"((0,0),(1,1))"`; another operand of `<->`, an array of `tsquery`, positions and weights of `tsvector`, an array of `tsvector` and of `path` fail. Without the fix the test fails at `other-tsquery-operand.sql`, which passes.
- Docs: `docs/llms-full.txt` §8.3, `docs/unit-testing/index.md` ("How values are compared").
