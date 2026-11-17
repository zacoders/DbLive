---
type: chore
priority: medium
created: 2026-11-14
closed: 2026-11-14
breaking: true
---
# Public members that a user does not need

## Release note
**The deployment test is one line, and the members of the API that a user does not need are internal.**

To upgrade:

- `DeploymentTest` (was `DeployFixture`) is not an xUnit fixture: `DeployAsync(output)` gets the database from the fixture builder, deploys, drops the database when `dropDatabaseOnComplete` is true (also after a failed deployment) and disposes the builder. Remove `IAsyncLifetime` and its two methods from the test class and create the test in the test method: `[SqlDeployFact] public Task ReleaseDeploys() => new DeploymentTest(new MyFixtureBuilder(), dropDatabaseOnComplete: true).DeployAsync(_output);`. The constructor parameter `builderProvider` is now `fixtureBuilder`.
- `IDbLiveDA`, its `DropDbAsync` and `DbLiveBuilder.CreateDbLiveDA()` are internal. The xUnit fixtures drop their databases as before. Without xUnit, a database of a container fixture builder needs no drop: the container goes away when the test process ends. On a server of your own, use a database with a fixed name and drop it with your own SQL before the deployment, as "A test database without xUnit" in the docs shows (https://zacoders.github.io/DbLive/unit-testing/#a-test-database-without-xunit).
- `DbLiveTestFixture.Tester` is not nullable: `fixture.Tester` instead of `fixture.Tester!` (the `!` still compiles). It throws `InvalidOperationException` before xUnit has initialized the fixture.
- The properties of `DeployParameters` are `init`: set them in the object initializer or with `with { ... }`.
- `DefaultImage` of `MsSqlContainerFixtureBuilder` and `PostgreSqlContainerFixtureBuilder` is internal; each builder has the constructors `(project)` and `(project, image)`. A new major version of the database engine in a default image comes only in a major DbLive release.

## Problem
After 0096-80, `DbLive` has 35 public types and 97 lines of public API. A third pass over the `PublicAPI.Shipped.txt` files (2026-11-14) compared every line with what a user calls: the docs, the template, the demos (verified with grep). No public type is left that a user does not need, but some members are:

- `IDbLiveDA.DropDbAsync`, the last member of `IDbLiveDA`, and with it `IDbLiveDA` and `DbLiveBuilder.CreateDbLiveDA()`. DbLive calls it from the fixtures of `DbLive.xunit` and for the internal option `RecreateDatabase` (`src/DbLive/DbLive.cs:27`); both see the internals. The docs call it in one recipe, the tests without xUnit (`docs/unit-testing/index.md`, Section 8.2 of `docs/llms-full.txt`), on a builder of `MsSqlContainerFixtureBuilder`: the container goes away when the test process ends (Section 11.6), so the drop is not needed there. The template, the demos and `.github/scripts/package-tests.cs` do not call it. Only two tests of the providers pass `skipIfNotExists: false`.
- `DeploymentTestFixture.InitializeAsync` and `DisposeAsync`. The deployment test creates the fixture with `new`, without `IClassFixture`, so every test class implements `IAsyncLifetime` only to forward the two calls: the template, the three demos, `docs/unit-testing/deployment-tests.md`, Section 13.3. `DeployAsync` can get the builder, deploy, drop the database and dispose the builder by itself. Its constructor parameter is `builderProvider`, the one of `DbLiveTestFixture` is `fixtureBuilder`; Section 13.3 passes it by name.
- `MsSqlContainerFixtureBuilder.DefaultImage` and `PostgreSqlContainerFixtureBuilder.DefaultImage`: public constants that nothing reads. With the default value of the `image` parameter they put the image into `PublicAPI.Shipped.txt`. Raising it (`postgres:18` to `postgres:19`, which the comment of the constant plans for a DbLive release) changes two lines of each file, and the "Release notes" check (`.github/scripts/release-notes/PublicApi.cs`) then requires a major version (read in the code, not run). So the analyzer decides whether a new image is a breaking change, as a side effect.
- The setters of `DeployParameters`. Nothing changes the parameters after creating them: the template, the demos, the docs and DbLive use an object initializer or `with`, for which `init` is enough.

The release is 2.0.0, which already breaks `IDbLiveDA` (0096-80). After it, each of these changes needs 3.0.0.

## Where
- `src/DbLive/Adapter/IDbLiveDA.cs`, `IDbLiveInternalDA.cs`, `src/DbLive/DbLiveBuilder.cs`; the `Bootstrapper` of each provider, which registers both interfaces
- `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs`; `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs` (drops through `CreateDbLiveDA()`)
- `src/DbLive.Testcontainers.MSSQL/MsSqlContainerFixtureBuilder.cs`, `src/DbLive.Testcontainers.PostgreSQL/PostgreSqlContainerFixtureBuilder.cs`
- `src/DbLive/DeployParameters.cs`
- The template (`MyDb.Tests/DeploymentTests.cs`) and the `DeploymentTests.cs` of the three demos
- Tests: `FixtureBuilderTests` (checks that `CreateDbLiveDA()` returns the instance of the internal interface), `DeploymentTestFixtureTests`, `DbLiveTestFixtureTests`, the provider tests that call `DropDbAsync`
- `PublicAPI.Shipped.txt` of `DbLive`, `DbLive.xunit` and the two Testcontainers packages

## Fix
- `IDbLiveDA`, `CreateDbLiveDA()` and `DropDbAsync` are internal. `IDbLiveDA` and `IDbLiveInternalDA` were split only to keep a public part, so they become one internal interface, registered once by each provider. The recipe without xUnit, decided by the maintainer 2026-11-14:
  - With a container builder there is nothing to drop: the container goes away with the test process.
  - On their own server: a database with a fixed name (`MyDb_Tests`), dropped with the user's own SQL before the deployment, so that every run deploys to an empty database. A database kept between runs gets an incremental deployment, and the deployment is not tested in full. Before and not after: a run that fails before its end leaves the database, and the next run drops it anyway.
  - The docs give the SQL for each provider. DbLive's connections stay in the pool, so a plain `drop database` fails while the database is in use. SQL Server, on `master`: `if db_id('MyDb_Tests') is not null begin alter database [MyDb_Tests] set single_user with rollback immediate; drop database [MyDb_Tests]; end`. PostgreSQL 13 or later, on `postgres`: `drop database if exists mydb_tests with (force);`. Verified, see Notes.
  - `DropDbAsync` does not stay public for this: a public drop is frozen until 3.0.0, and adding one later needs only a minor version. A fixture without xUnit that drops its database is a feature of its own, `tasks/ideas/tests-without-xunit-v2.md`.
- `DeploymentTestFixture` loses `InitializeAsync`, `DisposeAsync` and `IAsyncLifetime`. `DeployAsync(ITestOutputHelper)` gets the builder, deploys, drops the database when `dropDatabaseOnComplete` is true, and disposes the builder, also when the deployment fails; an error of the drop does not hide the error of the deployment. The parameter `builderProvider` is renamed `fixtureBuilder`. The test becomes `[SqlDeployFact] public Task ReleaseDeploys() => new DeploymentTestFixture(...).DeployAsync(output);`.
- `DefaultImage` is internal in both Testcontainers packages, and the constructor with the optional `image` becomes two: `(Assembly project)` and `(Assembly project, string image)`. The image is then not in the public API files, and the task that raises it decides whether that is a breaking change.
- The public properties of `DeployParameters` are `init` instead of `set`.

Open questions:

- [x] Without its lifecycle `DeploymentTestFixture` is no longer an xUnit fixture: rename it (`DeploymentTest`)? It was renamed from `DeployFixture` in this release already (0096-32-02). Yes, `DeploymentTest` (maintainer, 2026-11-14).
- [x] `DbLiveTestFixture.Tester` is `IDbLiveTester?`, so every `[SqlFact]` test writes `fixture.Tester!`. Make it non-null, throwing before `InitializeAsync`? The users' code still compiles; a line of `PublicAPI.Shipped.txt` changes, which is free in 2.0.0. Yes (maintainer, 2026-11-14).
- [x] Is a new major version of the database in the default image (`postgres:18` to `postgres:19`) a breaking change? A SQL test or the schema check can fail on it with no change on the user's side, which `docs/versioning.md` counts as breaking (see 0096-62). With `DefaultImage` internal, no check sees it; the answer goes to `docs/versioning.md`. Yes: a new major version of the engine in a default image comes only in a major DbLive release; an update within the same major version (`2025-latest`, `18.x`) is not breaking (maintainer, 2026-11-14).

Checked and kept:

- The properties of the exceptions, also those the docs do not name (`DatabaseNotFoundException.DatabaseName` and `Server`, `ScriptsNotEmbeddedException.AssemblyName`, `InvalidMigrationVersionException.FileName`, `ObsoleteMigrationSettingsFileException.FileName`, `RelativePath` of `BreakingScriptMatchesTestPatternException`, `InvalidMigrationItemSettingsException` and `MigrationSettingsWithoutScriptException`, `ScriptPathsDifferOnlyByCaseException.RelativePaths`), and `UnitTestsFailedException.FailedCount`, which equals `FailedTests.Count`. Nothing outside the tests reads them, and the message has their values. Kept public, decided by the maintainer 2026-11-14.
- `[SqlDeployFact]`, a `FactAttribute` with nothing added: the docs name the deployment test by it, and a later behavior of the attribute needs no change in the users' tests, where a plain `[Fact]` would.
- `IDbLiveFixtureBuilder` with both members: `[SqlFact]` discovery reads the project assembly without a database (`GetProjectAssembly`).
- `DbLiveBuilder.Dispose` and `DisposeAsync`; the members the compiler generates for the record `DeployParameters` (`with` needs `<Clone>$`).
- Decided before: `LogTo`, the connection string helpers, `DbLiveTestFixture.InitializeAsync` and `DisposeAsync` (0096-73, 0096-80).

## Docs to update
- `docs/llms-full.txt` §8.2 (the recipe without xUnit), §11 (the namespaces), §11.1 (`CreateDbLiveDA`), §11.4 (`IDbLiveDA`; its paragraph on `DbLiveSqlException` moves to §11.2 or §17), §11.6 (the constructors of the container builders), §13.3 (the deployment test)
- `docs/unit-testing/index.md` ("Running all tests without xUnit"), `docs/unit-testing/deployment-tests.md`
- `docs/versioning.md` (`IDbLiveDA` in the list of what the policy covers)
- The release note of `tasks/done/0096-80-public-api-not-used.md` (same release): its title and first item say that `IDbLiveDA` keeps `DropDbAsync`

## Done when
- [x] `IDbLiveDA`, `CreateDbLiveDA()` and `DropDbAsync` are internal; one internal interface for the database access of the providers
- [x] The recipe without xUnit: a database with a fixed name on the user's own server, dropped before the deployment with the SQL of each provider, verified on SQL Server and PostgreSQL after a deployment and a test run in the same process
- [x] `DeploymentTestFixture` has no `InitializeAsync`, `DisposeAsync` or `IAsyncLifetime`; `DeployAsync` owns the builder; the parameter is `fixtureBuilder`; the template, the demos and the docs use it so
- [x] `DefaultImage` is internal; each container builder has two constructors
- [x] The properties of `DeployParameters` are `init`
- [x] The open questions are answered, and what they decide is done
- [x] `PublicAPI.Shipped.txt` updated; the solution and `demo/Demo.sln` build, the tests pass
- [x] Docs updated

## Notes
- 2026-11-14, maintainer: `DeploymentTestFixture` is renamed `DeploymentTest`; `DbLiveTestFixture.Tester` is non-null; a new major version of the database engine in a default image is a breaking change and comes only in a major DbLive release (answers to the open questions).
- One internal interface: `IDbLiveInternalDA` took the name `IDbLiveDA` (namespace `DbLive.Adapter`) and `DropDbAsync`; each provider registers it once. `CreateDbLiveDA()` is removed, not made internal: the fixtures call `GetRequiredService<IDbLiveDA>()` as the rest of the internal code does.
- The recipe, verified with `TestsWithoutXunitRecipeTests` of both provider test projects: the SQL of the docs, a deployment and `RunAllTestsAsync`, a row inserted on a connection that stays in the pool (sessions on the database confirmed in `sys.dm_exec_sessions` / `pg_stat_activity`), then the recipe again in the same process, which must drop the row with the database.
  - PostgreSQL: the drop with `with (force)` works, but the next deployment in the process gets a connection that the drop closed from the Npgsql pool and fails with `57P01: terminating connection due to administrator command` (verified). SQL Server on Windows replaces such a connection, the second run passes without clearing the pool (verified); CI runs on Linux. So the recipe clears the pool of the test database on both providers after the drop.
  - `ClearAllPools` is wrong for it: in `DbLive.MSSQL.Tests` it made 10 tests that ran in parallel fail with "Implicit distributed transactions have not been enabled" (verified): their next connection in a `TransactionScope` came from a cleared pool, a new physical connection, and the transaction was promoted. The recipe calls `ClearPool(new SqlConnection(connectionString))` / `NpgsqlConnection.ClearPool(...)`: DbLive passes the connection string to the provider as it is (`DbLiveDbConnection`), so this is the pool of DbLive's connections.
- `DeploymentTest.DeployAsync`: when the deployment fails, the drop runs in the `catch` and the deployment error is rethrown; an error of the drop goes to the test output. The drop and the disposal of the builder now run inside the test, before they ran in `IAsyncLifetime.DisposeAsync` of the test class after it.
- `docs/llms-full.txt`: §11.4 `IDbLiveDA` is removed, its paragraph on `DbLiveSqlException` and the note that DbLive's database access is internal moved to §11.2; §11.5 and §11.6 are now §11.4 and §11.5 (the references in `docs/versioning.md` and §13 follow).
- Tests: the solution passes, but `DeploymentLoginPermissionsTests` of `DbLive.MSSQL.Tests` fails now and then in a full run, also on 435e5f2 before these changes (verified): its own cleanup drops the login while the killed sessions still exist. A task of its own, 0096-82.
- The release notes of the tasks of the same release that named `DeploymentTestFixture`, `builderProvider` or `IDbLiveDA` as public now name the final API: 0096-32-02 (its upgrade code), 0096-45, 0096-57, 0096-73, 0096-80 (title and first item).

## Result
- `IDbLiveDA` is one internal interface (the former `IDbLiveInternalDA` with `DropDbAsync`), registered once by each provider; `DbLiveBuilder.CreateDbLiveDA()` is removed.
- `DeploymentTestFixture` is `DeploymentTest`: no `IAsyncLifetime`, `InitializeAsync` or `DisposeAsync`; `DeployAsync` gets the builder, deploys, drops the database (also after a failed deployment, without hiding its error) and disposes the builder; the parameter is `fixtureBuilder`. The template, the three demos, `SchemaCheckTests` of both providers and the docs create it in the test method.
- `DbLiveTestFixture.Tester` is `IDbLiveTester`, throwing `InvalidOperationException` before `InitializeAsync`; the template, the demos, the READMEs and the docs drop the `!`.
- `DefaultImage` is internal in both Testcontainers packages; each container builder has `(project)` and `(project, image)`.
- `DeployParameters`: `init` instead of `set`.
- `PublicAPI.Shipped.txt` of `DbLive`, `DbLive.xunit` and both Testcontainers packages.
- Docs: the recipe for a test database without xUnit (llms-full §8.2, `docs/unit-testing/index.md`), llms-full §11, §11.1, §11.2, §11.5 (the container builders and the default image), §13.3, `docs/unit-testing/deployment-tests.md`, `docs/versioning.md` (the default images are covered by the policy).
- Tests: `DeploymentTestTests` (renamed from `DeploymentTestFixtureTests`; the drop and the disposal in `DeployAsync`, the error of the deployment when the drop fails too), a test of `Tester` in `DbLiveTestFixtureTests`, `TestsWithoutXunitRecipeTests` on SQL Server and PostgreSQL.
