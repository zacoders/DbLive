---
type: bug
priority: medium
created: 2026-11-24
---
# PostgreSQL: a SQL test fails on an array of numbers, dates or ranges with a NULL element

## Problem
Npgsql reads an array of a .NET value type with `GetValue` as an array of non-nullable elements (`int[]`), and throws when the array has a NULL element. DbLive reads every value of every result set with `GetValue` (`PostgreSqlDA.ReadValue`), so a SQL test with such a value fails, with any assert and without one:

`Cannot read a non-nullable collection of elements because the returned array contains nulls. Call GetFieldValue with a nullable collection type instead.`

The message tells the user to call a method that only DbLive calls. `array_agg` of a nullable column gives such a value.

Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18` (0096-154): `array[1, null]::int[]`, `array[true, null]`, `array[1.5, null]::numeric[]`, `array['2026-01-01'::date, null]`, `array['(0,0)'::point, null]`, `array['[1,2)'::int4range, null]` and `array['((0,0),(1,1))'::path, null]` fail. `array['a'::text, null]` is read, as `string[]` with a `null`; so is `inet[]` (0096-145) and, since 0096-154, `tsquery[]` and `tsvector[]`.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`, `ReadValue`

## Fix
- Read an array column whose element is a .NET value type as an array of nullable elements: `GetFieldValue<T?[]>` by `reader.GetFieldType(ordinal)`, as `inet[]` is read as `NpgsqlInet?[]`.
- `Array Nullability Mode=PerInstance` in the connection string does the same, but DbLive does not change the connection string of the user.
- Or docs only: select the array as `text`, or without the NULL elements.

- [ ] Does the text of `rows` stay the same for an array without NULL: `int?[]` `[1,2]` against `int[]` `[1,2]`?

## Docs to update
- `docs/llms-full.txt` §8.3
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [ ] Fixed, with a test: arrays of `int`, `numeric`, `date` and a range with a NULL element are compared in `rows`
- [ ] Docs updated

## Notes
- Found in 0096-154: a `path[]` with a NULL element failed the read before `ReadValue` got to it.
