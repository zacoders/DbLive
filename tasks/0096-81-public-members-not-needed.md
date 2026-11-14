---
type: chore
priority: medium
created: 2026-11-14
breaking: true
---
# Public members that a user does not need

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
  - The docs give the SQL for each provider. DbLive's connections stay in the pool, so a plain `drop database` fails while the database is in use. SQL Server, on `master`: `if db_id('MyDb_Tests') is not null begin alter database [MyDb_Tests] set single_user with rollback immediate; drop database [MyDb_Tests]; end`. PostgreSQL 13 or later, on `postgres`: `drop database if exists mydb_tests with (force);`. Not verified yet.
  - `DropDbAsync` does not stay public for this: a public drop is frozen until 3.0.0, and adding one later needs only a minor version. A fixture without xUnit that drops its database is a feature of its own, `tasks/ideas/tests-without-xunit-v2.md`.
- `DeploymentTestFixture` loses `InitializeAsync`, `DisposeAsync` and `IAsyncLifetime`. `DeployAsync(ITestOutputHelper)` gets the builder, deploys, drops the database when `dropDatabaseOnComplete` is true, and disposes the builder, also when the deployment fails; an error of the drop does not hide the error of the deployment. The parameter `builderProvider` is renamed `fixtureBuilder`. The test becomes `[SqlDeployFact] public Task ReleaseDeploys() => new DeploymentTestFixture(...).DeployAsync(output);`.
- `DefaultImage` is internal in both Testcontainers packages, and the constructor with the optional `image` becomes two: `(Assembly project)` and `(Assembly project, string image)`. The image is then not in the public API files, and the task that raises it decides whether that is a breaking change.
- The public properties of `DeployParameters` are `init` instead of `set`.

Open questions:

- [ ] Without its lifecycle `DeploymentTestFixture` is no longer an xUnit fixture: rename it (`DeploymentTest`)? It was renamed from `DeployFixture` in this release already (0096-32-02).
- [ ] `DbLiveTestFixture.Tester` is `IDbLiveTester?`, so every `[SqlFact]` test writes `fixture.Tester!`. Make it non-null, throwing before `InitializeAsync`? The users' code still compiles; a line of `PublicAPI.Shipped.txt` changes, which is free in 2.0.0.
- [ ] Is a new major version of the database in the default image (`postgres:18` to `postgres:19`) a breaking change? A SQL test or the schema check can fail on it with no change on the user's side, which `docs/versioning.md` counts as breaking (see 0096-62). With `DefaultImage` internal, no check sees it; the answer goes to `docs/versioning.md`.

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
- [ ] `IDbLiveDA`, `CreateDbLiveDA()` and `DropDbAsync` are internal; one internal interface for the database access of the providers
- [ ] The recipe without xUnit: a database with a fixed name on the user's own server, dropped before the deployment with the SQL of each provider, verified on SQL Server and PostgreSQL after a deployment and a test run in the same process
- [ ] `DeploymentTestFixture` has no `InitializeAsync`, `DisposeAsync` or `IAsyncLifetime`; `DeployAsync` owns the builder; the parameter is `fixtureBuilder`; the template, the demos and the docs use it so
- [ ] `DefaultImage` is internal; each container builder has two constructors
- [ ] The properties of `DeployParameters` are `init`
- [ ] The open questions are answered, and what they decide is done
- [ ] `PublicAPI.Shipped.txt` updated; the solution and `demo/Demo.sln` build, the tests pass
- [ ] Docs updated
