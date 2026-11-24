---
type: docs
priority: low
created: 2026-11-24
---
# PostgreSQL `regclass`, `pg_snapshot` and `aclitem` cannot be read in a SQL test, and the docs do not say so

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

- [ ] Other `reg*` types (`regtype`, `regproc`, `regnamespace`, `regrole`) and `txid_snapshot`: the same? Check them with the same query.

## Docs to update
- `docs/llms-full.txt` §8.3
- `docs/unit-testing/index.md` ("How values are compared")

## Done when
- [ ] Docs updated

## Notes
- Found in 0096-154. A task of its own, for the docs (the maintainer, 2026-11-24).
