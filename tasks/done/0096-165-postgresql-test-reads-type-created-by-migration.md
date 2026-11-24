---
type: bug
priority: high
created: 2026-11-24
closed: 2026-11-24
---
# PostgreSQL: a test that selects a column of an enum or a domain created by a migration fails

## Release note
**PostgreSQL: SQL tests in the process of the deployment read the enums and domains it created.** A SQL test that ran after a deployment in the same process, as `[SqlFact]` and `[SqlDeployFact]` of `DbLive.xunit` run them, failed on a column or a value of an enum, a domain or an array of an enum that the deployment created: `The given key '<oid>' was not present in the dictionary.` or `Reading as 'System.Object' is not supported for fields having DataTypeName '-.-'`. Npgsql loads the types of the database once for a connection string, before the migrations of the deployment. After a deployment that succeeded DbLive now reloads them for its connection string, so the connections of your application in the same process with the same connection string read the new types too. An `NpgsqlDataSource` of your application keeps types of its own: open its first connection after the deployment, or call its `ReloadTypesAsync()`. Nothing to change on upgrade.

## Problem
A migration creates an enum or a domain and a table with a column of it; a SQL test of the same project selects that column. Deployed and tested in one process, as `DbLive.xunit` does (deploy, then the tests), the test fails with `The given key '<oid>' was not present in the dictionary.` Verified on 2026-11-24 with Npgsql 10.0.1 and `postgres:18`: `DeployAsync(DeployParameters.Default)`, then `RunTestAsync` of `select v from t3; select 'rows' as assert; select cast('happy' as mood) as v;`, once with `create type mood as enum (...)`, once with `create domain ts0d as timestamp(0)`. Both fail.

Npgsql loads the types of the database into the data source of the connection string when it first opens a connection, before the migrations run. `PostgreSqlDA.ReadResultAsync` reads the result sets with `CommandBehavior.KeyInfo` and calls `GetColumnSchemaAsync`; Npgsql looks the type of the table column up by its OID and does not know the new type (`DbColumnSchemaGenerator.LoadColumnDefinition`, `KeyNotFoundException`). The same with an open connection: a type created on it after `OpenAsync` fails the same way until `ReloadTypesAsync`.

A test that checks a function or a table with an enum is common on PostgreSQL, so every such project fails its tests right after the deployment.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:435` (`GetColumnSchemaAsync` in `ReadResultAsync`)
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:106` (`CreateConnection`, one data source per connection string)
- `src/DbLive/DbLive.cs:60` (`DeployAsync`, the end of the deployment)

## Fix
Reload the types of the data source after the deployment changed the database: `NpgsqlConnection.ReloadTypesAsync()` once after the migrations (and the code?), or before the first test of a run.

- [x] Where to reload: at the end of the deployment in `PostgreSqlDA`, or before the tests? A test run without a deployment in the process (the database deployed by another process) loads the types fresh, so it needs no reload (verified, see Notes). At the end of a deployment that succeeded: approach 1 of Notes, the decision of 2026-11-24.
- [x] Does a test that reads such a value without `KeyInfo` fail too (a composite type, an enum array)? Then the reload must come before the reader, not only the column schema. Yes: an enum, an enum array and a composite type fail without `KeyInfo` with `InvalidCastException: Reading as 'System.Object' is not supported for fields having DataTypeName '-.-'`, also a value of an expression (`select 'happy'::mood`), with or without `KeyInfo`. A domain does not: the server sends its base type in the row description, and only the column schema of a table column looks up the domain (verified, see Notes). So the reload must come before the reader.
- [x] `ReloadTypesAsync` reloads the types for the whole data source, also for the connections of the user's code in the same process with the same connection string: harmless, it only adds the new types. Note it in the docs if it is visible. Visible, and only for the better: a connection of the process with the same connection string, opened after the reload, reads the new types, also a pooled one that was open before (verified). Without the reload an application that deploys at its startup and reads an enum column created by the deployment with `new NpgsqlConnection(sameConnectionString)` fails the same way until it restarts (the plain read, verified). An `NpgsqlDataSource` that the application builds has a type cache of its own, which the reload does not touch: one that opened a connection before the deployment fails with that `InvalidCastException` until its own `ReloadTypesAsync()`, one that opens its first connection after the deployment reads the type (verified on 2026-11-24, a temporary test). In `docs/llms-full.txt` §11.1 and §16, `docs/deployment/index.md`.

## Docs to update
- `docs/llms-full.txt` §11.1 (`SetDbConnection`: the connections of the process with the same connection string), §16 (provider differences). Not §8: the SQL tests just work.
- `docs/deployment/index.md` (after "DbLive in the process of your application (SQL Server)")

## Done when
- [x] Fixed, with a test: a migration creates an enum and a domain, a test in the same process selects their columns and passes
- [x] Docs updated

## Notes
- Found on 2026-11-24 while checking 0096-153 with experiments.
- Experiments of 2026-11-24 (verified), Npgsql 10.0.1, `postgres:18` in Docker, a temporary test class in `DbLive.PostgreSQL.Tests` (removed). The migration: `create type mood as enum`, `create domain ts0d as timestamp(0)`, `create type pair as (a int, b text)`, a table `t3 (v mood, d ts0d, p pair, va mood[])` with a row.
  - Deployed with `DeployParameters.Default`, then `RunTestAsync` with the same builder: `select v` (enum), `select d` (domain), `select p` (composite), `select va` (enum array) fail with `KeyNotFoundException: The given key '<oid>' was not present in the dictionary.`; `select 'happy'::mood, array['ok']::mood[], row(1,'x')::pair` fails with `InvalidCastException: Reading as 'System.Object' is not supported for fields having DataTypeName '-.-'`. The problem of the task is reproduced.
  - Plain Npgsql, the types created on another connection string after the first open of this one: without `KeyInfo` the enum, the enum array, the composite type and the values of expressions fail with that `InvalidCastException`; the domain reads (the row description has the base type). With `KeyInfo` the table columns fail with `KeyNotFoundException`, also the domain. The same with `Pooling=false`: the unpooled data source keeps its types as well.
  - `NpgsqlConnection.ReloadTypesAsync()` on one connection of the connection string, then three connections open at once (the connectors of the pool opened before the reload among them): all read the enum and the domain with `KeyInfo`. Also with `Pooling=false`. The tests of the first point pass after the reload, but for the composite type (below). The reload took 5 ms (4 ms the second time) on the small database of the test.
  - The database deployed through another connection string (another data source, as another process), the tests through the connection string of the test: the enum passes without a reload. A test run in a process without the deployment needs no reload.
  - A type created by `setup.test.sql` of the test (in the transaction of the test) cannot be read by the test: `InvalidCastException`, also after a reload at the end of the deployment. A `ReloadTypesAsync` inside the `TransactionScope`, after the script that created the type, reads it; after the rollback of the scope the data source knows a type that no longer exists, and a later connection works.
- Found while checking, not caused by the stale types (the types known to the data source):
  - A column of a composite type cannot be read in a SQL test at all: `InvalidCastException: Reading as 'System.Object' is not supported for fields having DataTypeName 'public.pair'`. Npgsql 8+ reads an unmapped composite type only with `EnableUnmappedTypes()` of a data source builder or of the global type mapper; DbLive uses neither. An anonymous `row(1, 'x')` reads.
  - `rows-with-schema` of a table column of a domain against `cast(... as ts0d)` fails: the column is `public.ts0d` (the column schema of a table column), the expected value `timestamp(0)` (an expression of a domain type has its base type). `rows` passes. An enum is `public.mood` on both sides and passes.
- Approaches (2026-11-24, for the maintainer):
  1. Reload at the end of a successful deployment: a method of `IDbLiveDA` that `DbLive.DeployAsync` calls after `_deployer.DeployAsync`, `ReloadTypesAsync` on PostgreSQL, nothing on SQL Server. Covers `DbLiveTestFixture`, `DeploymentTest` (also after the breaking runs) and an application that deploys at its startup. 5 ms per deployment. Not a type created by `setup.test.sql`.
  2. Reload in `PostgreSqlDA.ExecuteQueryMultipleAsync`, on the connection of the test before its reader. Also covers a type of `setup.test.sql`, not one of the test itself. One more query per test, also when nothing changed; does not help the application.
  3. Reload before the tests (`RunAllTestsAsync`, each `RunTestAsync`) in `DbLiveTester`: between 1 and 2, with the drawbacks of both.
- Decision of 2026-11-24 (the maintainer): approach 1. A type created by `setup.test.sql` or by the test itself stays as it is, no fix and no docs. The composite type and `rows-with-schema` of a domain column are tasks of their own: 0096-166, 0096-167.
- Review of the code by the maintainer (2026-11-24): a method of `IDbLiveDA` that only PostgreSQL needs, called by the deployment while the tests need it. Proposed then: a reload inside `PostgreSqlDA` before the first test after a deployment (a flag set by `AcquireDeploymentLockAsync`), or in `DbLiveTester` (again a method of `IDbLiveDA`, and `DbLiveTestFixture` calls `RunTestAsync` for each test). A reload before every test was ruled out by a measurement (verified 2026-11-24, a temporary test): a trivial SQL test takes 2.7 ms, `ReloadTypesAsync` 4.4 ms on an empty database and 12.8 ms with 300 enums, 300 domains and 300 tables. Decision of 2026-11-24 (the maintainer): keep the call at the end of the deployment, without the extra state in the data access, and name the method by the event, `OnDeployedAsync()`.

## Result
- `IDbLiveDA.OnDeployedAsync()`, called by `DbLive.DeployAsync` after the project is deployed, inside the deployment lock; not after a failed deployment. PostgreSQL: `NpgsqlConnection.ReloadTypesAsync()` on a connection of the connection string, about 5 ms. SQL Server: nothing, SqlClient keeps no types of the database (an alias type comes as its base type, verified by the test below).
- Decisions of 2026-11-24 (the maintainer): reload at the end of the deployment (approach 1); a type created by `setup.test.sql` or by the test itself is not handled. Found while checking: 0096-166 (a composite type cannot be read in a SQL test), 0096-167 (`rows-with-schema` of a domain column).
- Tests: `DbLive.PostgreSQL.Tests/SqlTestResultSetTests.Tests_in_the_process_of_the_deployment_read_the_types_it_created` (an enum, a domain and an enum array of a migration, a SQL test with `rows` in the process of the deployment, then a read of a plain `NpgsqlConnection`; failed with `KeyNotFoundException` before the fix), the same test in `DbLive.MSSQL.Tests` with an alias type and `rows-with-schema`, `DbLive.Tests/Deployers/DbLiveTests` (`Data_access_is_told_after_the_project_is_deployed`, `Data_access_is_not_told_when_the_deployment_fails`).
- Docs: `docs/llms-full.txt` §11.1 (`SetDbConnection`), §16; `docs/deployment/index.md` (note "Types created by a deployment (PostgreSQL)").
