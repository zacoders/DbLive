---
type: chore
priority: low
created: 2026-11-22
closed: 2026-11-22
---
# A base class for the tests that deploy a database of their own

## Release note
None. The integration tests of the repository only.

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
- [x] The classes with a database of their own derive from the base and drop their database; `SqlServerIntegrationTestBase` is gone
- [x] No copy of `CreateBuilder(TempProject)` and of the query helpers is left in these classes
- [x] All tests of both projects pass
- [x] A full run of `DbLive.PostgreSQL.Tests` leaves no `dblive--` database: checked with a query of `pg_database` before the container stops

## Notes
Verified on 2026-11-22 with a temporary test class in `DbLive.PostgreSQL.Tests`, removed after:
- `IAssemblyFixture<PostgreSqlFixture>` on an abstract base is enough: the derived class gets the fixture, and `InitializeAsync` and `DisposeAsync` of the base run.
- CS9107 and xUnit1041 fail the build as described under Fix.
- Two loggers on one builder (`AddXUnitLogger`, then `LogToXUnitOutput`) do not double the log: the second registration of `ILogger` replaces the first. One `CreateBuilder` with the logger fits `SchemaCheckTests` too, where `DeploymentTest` adds its own.
- An exception in `DisposeAsync` fails the test. When the test fails too, xUnit reports both, the error of the test first.
- `DropDbAsync` of a small database: 10-35 ms, 13 ms on average over 10 runs. About a second for a run of 70 tests.
- With `.WithReuse(true)` in `PostgreSqlFixture`, the container stays after the run, stopped (`Exited (0)`); without it, the fixture removes the container in `DisposeAsync`.

Decisions taken while moving the classes, 2026-11-22:
- The bases are `PostgreSqlDbTests` and `SqlServerDbTests`, files that end in `Tests.cs`: `src/.editorconfig` turns CA2007 off only for `*Tests.cs`, so a `...TestBase.cs` would need `ConfigureAwait(false)` on every `await` of the base. `DbLiveDATestExtensions.cs` is not such a file: its two new helpers call `ConfigureAwait(false)`. It changes only the continuations inside the helpers, and the test method still resumes on the context of xUnit.
- `InitializeAsync` and `DisposeAsync` of the bases are virtual. `CrossPlatformDeployTests` of SQL Server overrides `InitializeAsync` to create its database with a case-sensitive collation; `SchemaSnapshotTests` (both providers) and `MsSqlDeployerTests` override it to keep the data access of the test in a field.
- `ConcurrentDeploymentTests` of PostgreSQL keeps locking the database `postgres`. The class passes `createDb: false`, because its deployments create the database, and its two tests of the lock connection deploy nothing: on the database of the test they would first have to create it. Only the reason in the comment changed.
- `DeploymentLoginPermissionsTests` and `MissingDatabaseTests` of SQL Server keep a builder of their own, `CreateLoginBuilder`: the database of the test with the connection string of another login. `SetDbConnection` on the builder of the base would add a second `IDbLiveDbConnection` instead of replacing the first.
- `MsSqlDeployerTests` derived from `IntegrationTestsBase` of `DbLive.Tests.Common` and left its database on the server: it is on the base now. `PgSqlDeployerTests` stays on `IntegrationTestsBase`: it works in the database `postgres` of the fixture and creates none.
- `DatabaseExistsAsync` of `InvalidProjectTests` and `MissingDatabaseTests` became `DbExistsAsync()` of the base, not a helper of `DbLiveDATestExtensions`: it asks the server, not the data access of DbLive, about the database of the test.
- The build does not report IDE0005 (unused `using`): it needs `GenerateDocumentationFile`. Found with both turned on for one build and removed in the changed files, also the ones unused before this task.

Verified on 2026-11-22, after the change:
- `DbLive.PostgreSQL.Tests`: 78 of 78 passed in 6 runs without the poll inside the container below (one of them with a slower poll through `docker exec`), about 13 s each. A poll of `pg_stat_activity` inside the container every 50 ms: at most 44 client backends (43 and 44 in two runs), against 100 of `max_connections` before (0096-103). At most 14 `dblive--` databases at once. With `.WithReuse(true)` for the check: after the run, the stopped container started again has no `dblive--` database.
- With that poll, 2 of 6 runs had one failed test. The one captured: `TransactionTimeoutTests.Setup_and_test_that_each_fit_into_UnitTestItemTimeout_pass_together`, "The script ran longer than its timeout of 00:00:03" for a `pg_sleep(2)`. The class runs alone after the others (`DisableParallelization`), so the poll, a `psql` started every 50 ms, was the only load; the test leaves a margin of 1 second. Without that poll it passed in all 6 runs.
- `DbLive.MSSQL.Tests`: 155 of 155 passed in 4 of 5 runs, 65-68 s each. In the fifth, `CodeRetryInDeploymentTransactionTests.Code_file_that_outlives_the_deployment_timeout_fails_the_deployment_with_its_path` got no `ExecutionTimeoutException`: the timer of System.Transactions fired after the 4 s of its script, the delay that `TransactionTimeoutTests` describes. Before this task (a worktree of the previous commit) 1 of 5 runs had a failed test too; which one was not captured. With `.WithReuse(true)` for the check: after the run, the only user database is `DbLive-PersistedTest`, which its class keeps.
- The drop makes the run of `DbLive.MSSQL.Tests` longer: 41 s with the drop of the base turned off, 43-47 s before this task. `DropDbAsync` of SQL Server waits about 2.8 s for Query Store in `set single_user`: found here, see 0096-105. On PostgreSQL the drop costs about a second per run (the notes above).

## Result
- `PostgreSqlDbTests` and `SqlServerDbTests`, abstract bases in the test projects with `IAssemblyFixture<>` and `IAsyncLifetime`: `Fixture`, `Output`, `DbCnnString` with a new random database name, `DbName`, `CreateBuilder()` with the logger and without a project, `CreateBuilder(TempProject)`, `DbExistsAsync()`. `InitializeAsync` creates the database unless `createDb: false`, `DisposeAsync` drops it with `DropDbAsync`. The SQL Server base also takes `dbName` and `keepDb`, for `DeploySqlIntegrationPersistedTests`.
- 16 classes of `DbLive.PostgreSQL.Tests` and 30 of `DbLive.MSSQL.Tests` derive from them, the 5 `DeploySqlIntegration*` classes and `MsSqlDeployerTests` among them. `createDb: false`: `ConcurrentDeploymentTests`, `InvalidProjectTests`, `MissingDatabaseTests`, `NewerDbLiveDatabaseTests`, `ProjectIdTests` and `SchemaCheckTests` of both providers, and on SQL Server also `AccentInsensitiveCollationTests`, `DbLiveTablesDateTimeTests`, `DeploymentLoginPermissionsTests`, `OlderBuildDeployTests` and `SqlServerAndPostgreSqlInOneProcessTests`.
- `SqlServerIntegrationTestBase` is deleted, with its unused `DropTestingDatabases()`. Its classes take the deployer and the tester from `CreateBuilder().SetProject(Assembly.Load("DemoMSSQL"))`.
- `ScalarAsync` and `ListAsync` of `DbLiveDATestExtensions`, in both projects, replace the copies of the query helpers (`ScalarAsync`, `CountAsync` and `ListAsync` of the classes).
- 0096-103 is closed with this task: the drop ends the sessions of the database of each test, and a run stays at 44 client backends of 100.
- Found: the drop on SQL Server waits for Query Store, 0096-105. Until it is fixed, a run of `DbLive.MSSQL.Tests` takes about 20 s longer.
- Tests: see the last notes above.
