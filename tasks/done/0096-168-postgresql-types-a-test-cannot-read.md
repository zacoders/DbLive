---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# PostgreSQL `regclass`, `pg_snapshot` and `aclitem` cannot be read in a SQL test, and the docs do not say so

## Release note
None.

## Problem
DbLive reads every value of every result set of a SQL test (`PostgreSqlDA.ReadValue`, `GetValue`), so a value that Npgsql cannot read fails the test, with any assert and without one. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18` (0096-154):

- `'pg_class'::regclass`: `Reading as 'System.Object' is not supported for fields having DataTypeName 'regclass'`.
- `'10:20:10,14'::pg_snapshot`: the same, with `'pg_snapshot'`.
- `'postgres=r/postgres'::aclitem`: `42883: no binary output function available for type aclitem`.

The test fails, so nothing passes by mistake. But `docs/llms-full.txt` §8.3 lists the values that cannot be read (a `numeric` `NaN`, an `interval` with months) without these types, and the error does not say what to do: select the value as `text` (`'users'::regclass::text`).

## Where
- `docs/llms-full.txt` §8.3
- `docs/unit-testing/index.md` ("How values are compared")

## Fix
Name these types in the docs, with the workaround: select the value as `text`.

- [x] Other `reg*` types (`regtype`, `regproc`, `regnamespace`, `regrole`) and `txid_snapshot`: the same? Check them with the same query. `regproc`, `regprocedure`, `regoper`, `regoperator`, `regnamespace`, `regrole`, `regdictionary`, `regcollation`, `txid_snapshot` and an array of `regclass` fail the same way. `regtype` and `regconfig` are read, as their OID (`UInt32`): `'int4'::regtype` and `pg_typeof(1)` are 23 (verified on 2026-11-24, Npgsql 10.0.1, `postgres:18`, a temporary test, removed).

## Docs to update
- `docs/llms-full.txt` §8.3
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [x] Docs updated

## Notes
- Found in 0096-154. A task of its own, for the docs (the maintainer, 2026-11-24).
- Decision of 2026-11-24 (the maintainer): done together with 0096-166, whose approach 2 gives every value that cannot be read an error with the column and `Select the value as text in the test.`

## Result
- Docs: `docs/llms-full.txt` §8.3 names the types that cannot be read, with the workaround (select the value as text), `aclitem`, which the server fails, and `regtype` and `regconfig`, read as their OID; §17 a row for the `aclitem` error; `docs/unit-testing/index.md` ("How values are compared").
- The error of such a value names the column and says to select it as text: the change of 0096-166, whose release note covers both (the decision of 2026-11-24). No release note of its own.
- Tests: `regclass` and `pg_snapshot` in `DbLive.PostgreSQL.Tests/SqlTestResultSetTests.Test_that_cannot_read_a_value_names_the_column_and_says_to_select_it_as_text`, also `regclass::text`, which passes.
