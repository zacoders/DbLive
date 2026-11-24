---
type: bug
priority: low
created: 2026-11-24
---
# PostgreSQL: a SQL test cannot read a value of a composite type

## Problem
A SQL test that selects a column or a value of a composite type (`create type pair as (a int, b text)`) fails with `InvalidCastException: Reading as 'System.Object' is not supported for fields having DataTypeName 'public.pair'`, with any assert and without one: DbLive reads every value of every result set. Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18`, the type known to the data source (a test run in a process without the deployment, and after `ReloadTypesAsync`): a table column `p pair` and `select row(1,'x')::pair as p` both fail. An anonymous record (`select row(1, 'x') as r`) reads.

Npgsql 8 and later read an unmapped composite type only with `EnableUnmappedTypes()` of an `NpgsqlDataSourceBuilder` or of `NpgsqlConnection.GlobalTypeMapper`. DbLive opens its connections with `new NpgsqlConnection(connectionString)` and sets neither.

`docs/llms-full.txt` §8.3 says nothing of composite types. It names other values that cannot be read (an `interval` with months, a `numeric` `NaN`) and says to cast them to `text` in the test.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:463` (`ReadValue`, `reader.GetValue`)
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:106` (`CreateConnection`)

## Fix
- [ ] Docs only: the value cannot be read, cast it to `text` in the test or select its fields (`(p).a`)? Check what the text of a composite value is and that `rows` compares it.
- [ ] Or read it: `EnableUnmappedTypes()` on the global type mapper changes the whole process (as the Dapper settings in 0096-78), so not that one. An `NpgsqlDataSource` of DbLive's own with `EnableUnmappedTypes()` is another connection pool than the application's with the same connection string: does the enlistment in a `TransactionScope` still give every connection of a transaction the session of the transaction? What would `rows` compare then: the value as `object[]` or as text?
- [ ] Is `rows-with-schema` of such a column right after the fix (`public.pair` on both sides)?

## Docs to update
- `docs/llms-full.txt` §8.3 (values that cannot be read)
- `docs/unit-testing/index.md` ("Assertions"), if it lists such values

## Done when
- [ ] Fixed or documented, with a test
- [ ] Docs updated

## Notes
- Found on 2026-11-24 while checking 0096-165 with experiments (a temporary test class, removed).
