---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# PostgreSQL: `rows-with-schema` of a column of a domain never passes against a cast to the domain

## Release note
**PostgreSQL: `rows-with-schema` compares a column of a domain as its base type.** A table column of a domain (`create domain ts0 as timestamp(0)`) had the type of the domain (`public.ts0`), and an expected value the base type, also `cast(... as ts0)`: such a test never passed. Now a domain is compared as its base type with the length or precision of the domain (`timestamp(0)`), as an alias type on SQL Server; so is a domain over a domain, a domain over an enum is the enum, an array of a domain the array of its base type. A SQL test also gets the type of a column that it changed itself (`alter table ... alter column ... type`), not the committed one, and reads the column types without a connection of its own for each result set. Nothing to change on upgrade.

## Problem
A table column of a domain (`create domain ts0d as timestamp(0)`, `create table t3 (d ts0d)`) has the type `public.ts0d` in a SQL test, the expected value `cast('2026-01-01 10:00:00' as ts0d)` the type `timestamp(0)`. So `rows-with-schema` fails: `Columns does not match: Expected columns: d: timestamp(0) Actual columns: d: public.ts0d`. `rows` passes. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18`.

`PostgreSqlDA.GetPostgresTypeName` names the type by `NpgsqlDbColumn.DataTypeName`. For a table column the column schema (`CommandBehavior.KeyInfo`) reads its type from `pg_attribute`, which is the domain. For an expression the server sends the base type of the domain in the row description, so no expression in the test has the type of the domain. An enum has its own type on both sides (`public.mood`) and passes.

The expected side can only be a column of a table of the same domain, so a test cannot state the type of such a column.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:515` (`GetPostgresTypeName`)
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:421` (`ExecuteReaderAsync(CommandBehavior.KeyInfo, ...)` in `QueryMultipleAsync`)

## Fix
Name a domain by its base type, with the precision or length of the domain, as the server does for an expression: `public.ts0d` becomes `timestamp(0)`.

- [x] What does `NpgsqlDbColumn` give for a table column of a domain: `PostgresType` as `PostgresDomainType` with `BaseType`, and the precision and length of the domain or none? With `KeyInfo`: `PostgresDomainType`, `BaseType` the type under it, `DataTypeName` `public.ts0d`, and the precision, length and scale of the domain (`ts0d` over `timestamp(0)`: `NumericPrecision` 0; over `varchar(10)`: `ColumnSize` 10; over `numeric(10,2)`: 10 and 2). For a domain over a domain none: `BaseType` is the inner domain and the precision is null. Without `KeyInfo` the column has the base type with the modifier of the domain, as an expression has (verified, see Notes).
- [x] A domain over a domain, over an enum, over an array, an array of a domain? With `KeyInfo` each is named by the domain: `public.ts0d2`, `public.dmood`, `public.ints`, `public.ts0d[]`. The same expressions give `timestamp(0)`, `public.mood`, `integer[]`, `timestamp without time zone[]`, and so does the column read without `KeyInfo` (verified, see Notes).
- [x] `breaking`? A test that compared a column of a domain with a column of a table of the same domain passed and would still pass (both get the base type). One that compared with a column of another domain over the same base type failed and would pass. Not breaking with either approach of Notes: a test that passed still passes, both sides get the same name. One exception with approach 1: a test that changes the type of a column in its own script and expects the committed type (`alter table t alter column v type varchar(20)`, then `varchar(10)` expected) passed by mistake and fails.

## Docs to update
- `docs/llms-full.txt` §8.3 (`rows-with-schema`: a domain is compared as its base type)
- `docs/unit-testing/index.md` ("Assertions")

## Done when
- [x] Fixed, with a test: a table column of a domain against a cast to the domain passes `rows-with-schema`
- [x] Docs updated

## Notes
- Found on 2026-11-24 while checking 0096-165 with experiments (a temporary test class, removed).
- Experiments of 2026-11-24 (verified), Npgsql 10.0.1, `postgres:18` (18.6) in Docker, temporary test classes in `DbLive.PostgreSQL.Tests` (removed).
  - The problem is reproduced: a migration with `create domain ts0d as timestamp(0)` and a table column of it; `rows-with-schema` against `cast('2026-01-01 10:00:00' as ts0d)` fails with `Expected columns: d: timestamp(0)` / `Actual columns: d: public.ts0d`, `rows` passes.
  - `GetColumnSchemaAsync` with `KeyInfo` and without it, for table columns of `ts0d`, `ts0d2` (a domain over `ts0d`), `vc10` (`varchar(10)`), `n102` (`numeric(10,2)`), `dmood` (over an enum), `ints` (over `int[]`), `posint` (`int` with a check), `plaintext` (`text`), `ts` (`timestamp`), `ts0d[]`, and a domain in another schema (`other.ts0d`). DbLive names them with `KeyInfo`: `public.ts0d`, `public.ts0d2`, `public.vc10`, `public.n102`, `public.dmood`, `public.ints`, `public.posint`, `public.plaintext`, `public.ts`, `public.ts0d[]`, `other.ts0d`; the same through a view. Without `KeyInfo`: `timestamp(0)`, `timestamp(0)`, `varchar(10)`, `numeric(10,2)`, `public.mood`, `integer[]`, `integer`, `text`, `timestamp`, `timestamp without time zone[]`, `timestamp(0)`: the names of the casts to the same domains, and of the columns of a function `returns table (d ts0d, v vc10, ad ts0d[])` with or without `KeyInfo`. The server sends the base type of a domain and its modifier in the row description for every column (`getBaseTypeAndTypmod`); only the column schema of `KeyInfo` reads the type of a table column from `pg_attribute`.
  - With `KeyInfo` Npgsql reads the column schema on a connection of its own: 5 reads of the column schema of a table result set with `Pooling=false` ended 5 sessions of the database (`pg_stat_database.sessions`), 0 without `KeyInfo`. So it reads the committed catalog, not the one of the transaction of the test:
    - a table that the test creates is not found, and its columns get the type of the row description: a column of a domain of such a table is `timestamp(0)`, so the workaround "expected from a table of the same domain" works only with a table that is committed (of a migration), not with one or a temp table of the test;
    - a test that changes the type of a committed column gets the old type: `alter table t5 alter column v type varchar(20)` in the test, then `select v from t5` is `varchar(10)`, and `rows-with-schema` against `cast('a' as varchar(20))` fails with `Actual columns: v: varchar(10)`.
  - Cost of `KeyInfo`: 2.0 to 2.3 ms against 0.6 ms for a pooled connection, a one-row table result set and its column schema (200 reads, twice). A SQL test with a table in its actual and its expected result set pays it twice.
  - Without `KeyInfo` DbLive names every other type as with it: a table of 50 columns (`serial`, identity, the numeric, character, bit, date and time types with and without a precision, `interval hour to minute`, `json`, `jsonb`, `bytea`, the network types, `money`, `xml`, `point`, `tsvector`, `int4range`, `int[]`, `varchar(10)[]`, an enum and its array, `timestamp(0)[]`, a stored generated column, `oid`, `name`, `"char"`), aliases (`select i as alias_i, vc50 as "Alias"`), a join of two tables with the same column names, a partitioned table and its partition, a materialized view, a view, `group by`, a subquery, a CTE, `union all`: the same column names and type names (`GetPostgresTypeName` called on both column schemas). The only difference was a column of a domain.
  - SQL Server compares an alias type as its base type (`create type dbo.Code from nvarchar(20)` against `cast(N'A1' as nvarchar(20))` passes, 0096-165), and `MsSqlDA` reads without `KeyInfo`.
- Approaches (2026-11-24, for the maintainer):
  1. Read the result sets of a SQL test without `CommandBehavior.KeyInfo` (`QueryMultipleAsync`, one argument). The column schema comes from the row description: a domain is its base type with its modifier, for a table column, a view, a function and an expression alike, also a domain over a domain and an array of a domain. Also fixes the type of a column that the test itself creates or changes, saves a connection and about 1.5 ms per result set with table columns, and no type of a table column is looked up by OID any more (the `KeyNotFoundException` of 0096-165 for a domain). Small effort; the risk is a type whose name changes, none in the comparison above, and the whole PostgreSQL test suite runs. Not breaking but for the test that expects the committed type of a column it changed. No `dblive` tables. Docs: §8.3 and "Assertions" (a domain is compared as its base type, as an alias type on SQL Server), the comment on `SqlTestResultSetTests` (it names `KeyInfo`). Test: a table column of `ts0d`, of a domain over it, of `vc10` and of `ts0d[]` against casts to the domains pass `rows-with-schema`; a column of a domain against another base type fails with the base type in the message.
  2. Keep `KeyInfo` and name a `PostgresDomainType` by its `BaseType` in `GetPostgresTypeName` (also the element of an array of a domain, by the long name without the modifier, as Npgsql names arrays). A domain over a domain has no precision with `KeyInfo` (verified), so `ts0d2` would be `timestamp` against `timestamp(0)` of the cast and still fail, unless DbLive reads the modifier from `pg_type` itself. The tables of the test and the cost of `KeyInfo` stay. More code than 1, not breaking.
  3. Docs only: a column of a domain passes `rows-with-schema` only against a column of a committed table of the same domain, or use `rows`. Leaves the gap: a test cannot state the type with an expression, and the docs would describe a limitation that 1 removes in one line.
  - Recommended: 1.
- Decision of 2026-11-24 (the maintainer): approach 1, not breaking (the test that expects the committed type of a column it changed passed by mistake), and the column types of the transaction of the test in this task.

## Result
- `PostgreSqlDA.QueryMultipleAsync` reads the result sets of a SQL test without `CommandBehavior.KeyInfo`. The column schema comes from the row description of the server: a domain is its base type with the modifier of the domain (`timestamp(0)`, `varchar(10)`, `numeric(10,2)`), also a domain over a domain; a domain over an enum is the enum, over an array the array, an array of a domain the array of the base type (`timestamp without time zone[]`). A column whose type the test changed has the new type. No connection of Npgsql's own per result set any more. `GetPostgresTypeName` is unchanged. The comment of `OnDeployedAsync` no longer names the `KeyNotFoundException` of the column schema.
- Decisions of 2026-11-24 (the maintainer): approach 1, not breaking, the types of the transaction of the test in this task.
- Tests (`DbLive.PostgreSQL.Tests/SqlTestResultSetTests.cs`): `Rows_with_schema_test_compares_a_domain_as_its_base_type` (table columns of domains over `timestamp(0)`, over that domain, over `varchar(10)`, `numeric(10,2)`, an enum and `int[]`, and of an array of a domain, against casts to the domains and to the base types pass; against `timestamp` and `text` fail with the base types in the message), `Rows_with_schema_test_takes_the_column_type_the_test_changed` (`varchar(10)` changed to `varchar(20)` in the test). Both failed with `KeyInfo` (verified by putting it back). The whole `DbLive.PostgreSQL.Tests` passed (106).
- Docs: `docs/llms-full.txt` §8.3 (`rows-with-schema`: a domain as its base type, a SQL Server alias type too, the types of the transaction of the test), `docs/unit-testing/index.md` ("Assertions"). §16 needs no row: the providers no longer differ here.
