---
type: bug
priority: high
created: 2026-11-24
---
# PostgreSQL: a test that selects a column of an enum or a domain created by a migration fails

## Problem
A migration creates an enum or a domain and a table with a column of it; a SQL test of the same project selects that column. Deployed and tested in one process, as `DbLive.xunit` does (deploy, then the tests), the test fails with `The given key '<oid>' was not present in the dictionary.` Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18`: `DeployAsync(DeployParameters.Default)`, then `RunTestAsync` of `select v from t3; select 'rows' as assert; select cast('happy' as mood) as v;`, once with `create type mood as enum (...)`, once with `create domain ts0d as timestamp(0)`. Both fail.

Npgsql loads the types of the database into the data source of the connection string when it first opens a connection, before the migrations run. `PostgreSqlDA.ReadResultAsync` reads the result sets with `CommandBehavior.KeyInfo` and calls `GetColumnSchemaAsync`; Npgsql looks the type of the table column up by its OID and does not know the new type (`DbColumnSchemaGenerator.LoadColumnDefinition`, `KeyNotFoundException`). The same with an open connection: a type created on it after `OpenAsync` fails the same way until `ReloadTypesAsync`.

A test that checks a function or a table with an enum is common on PostgreSQL, so every such project fails its tests right after the deployment.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:435` (`GetColumnSchemaAsync` in `ReadResultAsync`)
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:106` (`CreateConnection`, one data source per connection string)

## Fix
Reload the types of the data source after the deployment changed the database: `NpgsqlConnection.ReloadTypesAsync()` once after the migrations (and the code?), or before the first test of a run.

- [ ] Where to reload: at the end of the deployment in `PostgreSqlDA`, or before the tests? A test run without a deployment in the process (the database deployed by another process) loads the types fresh, so it needs no reload.
- [ ] Does a test that reads such a value without `KeyInfo` fail too (a composite type, an enum array)? Then the reload must come before the reader, not only the column schema.
- [ ] `ReloadTypesAsync` reloads the types for the whole data source, also for the connections of the user's code in the same process with the same connection string: harmless, it only adds the new types. Note it in the docs if it is visible.

## Docs to update
- `docs/llms-full.txt` §8 (SQL tests), if the reload is visible to the user

## Done when
- [ ] Fixed, with a test: a migration creates an enum and a domain, a test in the same process selects their columns and passes
- [ ] Docs updated

## Notes
- Found on 2026-11-24 while checking 0096-153 with experiments.
