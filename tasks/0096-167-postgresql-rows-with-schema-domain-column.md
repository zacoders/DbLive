---
type: bug
priority: low
created: 2026-11-24
---
# PostgreSQL: `rows-with-schema` of a column of a domain never passes against a cast to the domain

## Problem
A table column of a domain (`create domain ts0d as timestamp(0)`, `create table t3 (d ts0d)`) has the type `public.ts0d` in a SQL test, the expected value `cast('2026-01-01 10:00:00' as ts0d)` the type `timestamp(0)`. So `rows-with-schema` fails: `Columns does not match: Expected columns: d: timestamp(0) Actual columns: d: public.ts0d`. `rows` passes. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18`.

`PostgreSqlDA.GetPostgresTypeName` names the type by `NpgsqlDbColumn.DataTypeName`. For a table column the column schema (`CommandBehavior.KeyInfo`) reads its type from `pg_attribute`, which is the domain. For an expression the server sends the base type of the domain in the row description, so no expression in the test has the type of the domain. An enum has its own type on both sides (`public.mood`) and passes.

The expected side can only be a column of a table of the same domain, so a test cannot state the type of such a column.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:471` (`GetPostgresTypeName`)

## Fix
Name a domain by its base type, with the precision or length of the domain, as the server does for an expression: `public.ts0d` becomes `timestamp(0)`.

- [ ] What does `NpgsqlDbColumn` give for a table column of a domain: `PostgresType` as `PostgresDomainType` with `BaseType`, and the precision and length of the domain or none?
- [ ] A domain over a domain, over an enum, over an array, an array of a domain?
- [ ] `breaking`? A test that compared a column of a domain with a column of a table of the same domain passed and would still pass (both get the base type). One that compared with a column of another domain over the same base type failed and would pass.

## Docs to update
- `docs/llms-full.txt` §8.3 (`rows-with-schema`: a domain is compared as its base type)
- `docs/unit-testing/index.md` ("Assertions")

## Done when
- [ ] Fixed, with a test: a table column of a domain against a cast to the domain passes `rows-with-schema`
- [ ] Docs updated

## Notes
- Found on 2026-11-24 while checking 0096-165 with experiments (a temporary test class, removed).
