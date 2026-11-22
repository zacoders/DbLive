---
type: chore
priority: low
created: 2026-11-22
---
# A base class for the tests that deploy a database of their own

## Problem
The integration tests of both providers copy the same code into each class that deploys a database of its own: the field with a random database name, `InitializeAsync` that creates the database, an empty `DisposeAsync`, `CreateBuilder(TempProject)` and small query helpers. Almost no copy drops the database, so the tests leave their databases on the server, and on PostgreSQL the idle connections to them stay in the Npgsql pools to the end of the run (0096-103). A new test copies the code again and leaves its database too. 0096-103 limits the connections on its own; this task makes the drop the default of every test.

Counted on 2026-11-22.

`DbLive.PostgreSQL.Tests`, 16 classes with a random database (`SetRandomPostgreSqlDatabaseName`):
- `CreateBuilder(TempProject)`: 15 copies, 13 identical. `ScriptEndsTransactionTests` and `SchemaCheckTests` have no logger; their SQL Server twins have one, so it is not a decision.
- `InitializeAsync` that creates the database: 9 copies of the same line; each creates a `DbLiveBuilder` and does not dispose it. `DisposeAsync() => Task.CompletedTask`: 9.
- `ScalarAsync`/`CountAsync(IDbLiveDA, string)` with the same body: 6. `DatabaseExistsAsync` is the same in `InvalidProjectTests` and `MissingDatabaseTests`.
- Drop their database: `SchemaSnapshotTests` (its `DisposeAsync`) and `SchemaCheckTests` (each test, through `DeploymentTest` with `dropDatabaseOnComplete: true`). `MissingDatabaseTests` creates none.

`DbLive.MSSQL.Tests`, 25 classes with a random database (`SetRandomMsSqlDatabaseName`):
- `CreateBuilder(TempProject)`: 16 identical copies. `InitializeAsync` that creates the database: 12. `DisposeAsync() => Task.CompletedTask`: 13.
- Drop their database: `SchemaCheckTests` and `SchemaSnapshotTests`. The 5 `DeploySqlIntegration*` classes derive from `SqlServerIntegrationTestBase`, another base: it deploys the Demo project, its synchronous constructor creates the database, and `Dispose` drops it unless `keepDatabaseAfterTests`.

## Where
- `src/DbLive.PostgreSQL.Tests/`: the classes above, `DbLiveDATestExtensions.cs`
- `src/DbLive.MSSQL.Tests/`: the classes above, `SqlServerIntegrationTestBase.cs` and its 5 `DeploySqlIntegration*` classes

## Fix
Both providers in one change. A base class in each test project, for example `PostgreSqlDbTests(PostgreSqlFixture fixture, ITestOutputHelper output, bool createDb = true) : IAssemblyFixture<PostgreSqlFixture>, IAsyncLifetime`:
- `DbCnnString`: a new random database name.
- `InitializeAsync` creates the database when `createDb`. `ConcurrentDeploymentTests`, `InvalidProjectTests`, `MissingDatabaseTests`, `NewerDbLiveDatabaseTests`, `ProjectIdTests` and `SchemaCheckTests` pass `false`: the deployment creates the database (`CreateDbIfNotExists`), and `InvalidProjectTests` and `MissingDatabaseTests` check that a failed deployment created none.
- `DisposeAsync` calls `DropDbAsync()`. It ends the sessions of the database (`pg_terminate_backend` on PostgreSQL, `set single_user with rollback immediate` on SQL Server), so the connections of a pool with another connection string end too, such as `MaxPoolSize = 1` in `ItemTransactionSettingsTests`.
- `CreateBuilder()` with the logger and without a project, `CreateBuilder(TempProject)` on top of it, `Output`, `Fixture`.
- The query helpers go to `DbLiveDATestExtensions`, not to the base: they are SQL of the tests through the data access of DbLive.

The SQL Server base replaces `SqlServerIntegrationTestBase`. Its 5 classes deploy the Demo project (`CreateBuilder().SetProject(...)`) instead of a `TempProject`. `DeploySqlIntegrationPersistedTests` needs two parameters of the base: a fixed database name (`DbLive-PersistedTest`) and keeping the database. Keeping it changes something only with `.WithReuse(true)`: then the next run deploys to the database of the previous one. The private `DropTestingDatabases()` of the old base (all `DbLive--%` databases) has no caller and goes away with it.

What changes in the derived classes:
- The test bodies use `Output`, `Fixture` and `DbCnnString` of the base. A class that passes `_output` or `_fixture` to the base and also uses it does not build: CS9107, an error with `TreatWarningsAsErrors`. On PostgreSQL: 8 uses of `_output`, 5 of `_fixture`, 51 of `_dbCnnString`.
- Each derived class keeps `[SuppressMessage("Usage", "xUnit1041:...")]`: the analyzer reports the fixture argument of the derived class also when the base has `IAssemblyFixture<>`.
- `ConcurrentDeploymentTests.AcquireLockOfDatabasePostgresAsync` locks the database `postgres` so that no pool of a test database stays open; with the drop it can lock the database of the test.

- [x] Both providers in one change, or PostgreSQL first? On SQL Server the leak does no harm (32,767 connections), but the twin classes of the two projects are built the same way now. Maintainer, 2026-11-22: both.
- [x] On SQL Server, one base together with `SqlServerIntegrationTestBase`, or a separate one? Maintainer, 2026-11-22: one, if nothing stands in the way. Checked: nothing does, see above.
- [x] Keep the database of a failed test for debugging, with a flag like `keepDatabaseAfterTests`? Maintainer, 2026-11-22: not now, add it when it is needed. xUnit 2 gives the test class no result of its test, so such a switch would keep the databases of all tests of the run, and only together with `.WithReuse(true)` of the fixture: without it the fixture removes the container, and the databases with it, at the end of the run.

## Done when
- [ ] The classes with a database of their own derive from the base and drop their database; `SqlServerIntegrationTestBase` is gone
- [ ] No copy of `CreateBuilder(TempProject)` and of the query helpers is left in these classes
- [ ] All tests of both projects pass
- [ ] A full run of `DbLive.PostgreSQL.Tests` leaves no `dblive--` database: checked with a query of `pg_database` before the container stops

## Notes
Verified on 2026-11-22 with a temporary test class in `DbLive.PostgreSQL.Tests`, removed after:
- `IAssemblyFixture<PostgreSqlFixture>` on an abstract base is enough: the derived class gets the fixture, and `InitializeAsync` and `DisposeAsync` of the base run.
- CS9107 and xUnit1041 fail the build as described under Fix.
- Two loggers on one builder (`AddXUnitLogger`, then `LogToXUnitOutput`) do not double the log: the second registration of `ILogger` replaces the first. One `CreateBuilder` with the logger fits `SchemaCheckTests` too, where `DeploymentTest` adds its own.
- An exception in `DisposeAsync` fails the test. When the test fails too, xUnit reports both, the error of the test first.
- `DropDbAsync` of a small database: 10-35 ms, 13 ms on average over 10 runs. About a second for a run of 70 tests.
- With `.WithReuse(true)` in `PostgreSqlFixture`, the container stays after the run, stopped (`Exited (0)`); without it, the fixture removes the container in `DisposeAsync`.
