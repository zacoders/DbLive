---
type: feature
priority: low
created: 2026-11-03
closed: 2026-11-03
---
# Ready-made fixture builders: local server and Testcontainers

## Release note
**Ready-made fixture builders: a test fixture is one line.** The new package `DbLive.Testcontainers.MSSQL` has `MsSqlContainerFixtureBuilder`. It starts SQL Server in Docker once per test run and gives every fixture a new database: `public class MyDbTestFixture() : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);`. `DbLive.Testcontainers.PostgreSQL` has `PostgreSqlContainerFixtureBuilder` for PostgreSQL. The packages bring the provider and Testcontainers. The default images are `mcr.microsoft.com/mssql/server:2025-latest` and `postgres:18`; the second argument takes another one. For a server you run, `MsSqlFixtureBuilder(assembly, connectionString)` in `DbLive.MSSQL` and `PostgreSqlFixtureBuilder` in `DbLive.PostgreSQL` give every fixture a new database on that server. All builders are in the namespaces `DbLive.MSSQL` and `DbLive.PostgreSQL`.

`IDbLiveFixtureBuilder` moved from the `DbLive.xunit` package to `DbLive`, namespace `DbLive`. A builder class of your own compiles unchanged when its file has `using DbLive;`. To switch to a ready-made builder, delete your builder class, and replace the `Testcontainers.MsSql` or `Testcontainers.PostgreSql` package reference of the test project with `DbLive.Testcontainers.MSSQL` or `DbLive.Testcontainers.PostgreSQL`.

## Goal
Every test project writes its own `IDbLiveFixtureBuilder` (`docs/unit-testing/index.md` "A fixture builder", `docs/llms-full.txt` §13.1). It is about 25 lines: start a container once per test run, give each fixture a database with a random name, call the provider and `SetProject`. The three demos have copies of it, and the docs show a second variant for a local server without Docker. With builders in the packages, the fixture is one line, and Testcontainers comes in as a dependency instead of a package reference the user adds by hand:

```csharp
// Docker
public class MyDbTestFixture()
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);

// Local server
public class MyDbTestFixture()
    : DbLiveTestFixture(
        new MsSqlFixtureBuilder(Assembly.Load("MyDb"), "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"),
        dropDatabaseOnComplete: true);
```

## Design
- `IDbLiveFixtureBuilder` moves from `DbLive.xunit` to `DbLive`, namespace `DbLive`. A builder class written by a user keeps compiling: it already has `using DbLive;` for `DbLiveBuilder`, and the `DbLive.xunit` namespace still exists (verified, Notes). The binary break does not matter, because all packages are released together with one version.
- **Local server:** `MsSqlFixtureBuilder` in `DbLive.MSSQL` and `PostgreSqlFixtureBuilder` in `DbLive.PostgreSQL`. They take the SQL project assembly and a connection string to the server, and give each `GetBuilderAsync` a new random database name. No new dependency:

  ```csharp
  namespace DbLive.MSSQL;

  public sealed class MsSqlFixtureBuilder(Assembly project, string connectionString) : IDbLiveFixtureBuilder
  {
      public Task<DbLiveBuilder> GetBuilderAsync() =>
          Task.FromResult(new DbLiveBuilder()
              .SqlServer()
              .SetDbConnection(connectionString.SetRandomMsSqlDatabaseName())
              .SetProject(project));

      public Assembly GetProjectAssembly() => project;
  }
  ```

- **Docker:** new packages `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL` with `MsSqlContainerFixtureBuilder` and `PostgreSqlContainerFixtureBuilder`. They depend on the provider package and on `Testcontainers.MsSql` / `Testcontainers.PostgreSql`, not on xUnit, so a future xUnit v3 package can use them too. `DbLive.xunit` gets no Testcontainers dependency, and a SQL Server project does not pull Npgsql.
  - They take the SQL project assembly and an optional image. The defaults are the images of the demos: `mcr.microsoft.com/mssql/server:2025-latest` and `postgres:latest`.
  - One container per image per test run, started on first use. They pass the container's connection string to the local server builder, which sets the random database name.
  - The start is thread safe. The demo builders check `State != Running` and then call `StartAsync`, so two fixtures that xUnit initializes in parallel can both call `StartAsync`. Testcontainers serializes the calls and starts one container (verified, Notes); the builders start it once anyway.

## Where
- `src/DbLive.xunit/IDbLiveFixtureBuilder.cs`, moves to `src/DbLive/`. Used by `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs`, `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs`, and the test builders in `src/DbLive.Tests/SqlTest/SqlFactDiscovererTests.cs` and `src/DbLive.Tests/Deploy/DeploymentTestFixtureTests.cs`.
- Random database names: `src/DbLive.MSSQL/MSSQLConnectionStringExtensions.cs`, `src/DbLive.PostgreSQL/PostgreSqlConnectionStringExtensions.cs`.
- Copies in the demos, to be replaced: `src/Demo/DemoMSSQL.Tests/DockerMsSqlFixtureBuilder.cs`, `src/Demo/AdventureWorks/AdventureWorks.Tests/DockerMsSqlFixtureBuilder.cs`, `src/Demo/Demo.PostgreSQL.Chinook.Tests/DockerPostgresFixtureBuilder.cs`.
- `src/Directory.Packages.props:28-29`: Testcontainers versions.
- `.github/workflows/cicd.yml:73-83`: pack the new packages with the others.
- "The four packages" in `docs/versioning.md:3`, `Development.md:7`, `docs/llms-full.txt` §2.

## Plan
- [x] Move `IDbLiveFixtureBuilder` to `DbLive`.
- [x] `MsSqlFixtureBuilder` and `PostgreSqlFixtureBuilder` in the provider packages, with unit tests: each `GetBuilderAsync` gets a new database name on the given server, and the project is the given assembly.
- [x] `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL` with a thread-safe container start, tested against Docker in `DbLive.MSSQL.Tests` and `DbLive.PostgreSQL.Tests`.
- [x] Demos use them.
- [x] Packed and published with the other packages.
- [x] Docs.

## Docs to update
- `README.md` ("Quick start", step 1: packages; "SQL testing")
- `docs/unit-testing/index.md` ("Running tests in Visual Studio and CI (xUnit)", including "Without Docker, point the builder at a local server"; "Running all tests without xUnit"), `docs/unit-testing/stored-proc-and-unit-test.md`
- `docs/llms-full.txt` §2, §3.2, §8.2, §11 (C# API, new §11.6), §13, §18.1
- `docs/versioning.md` (list of packages; `IDbLiveFixtureBuilder` is listed under `DbLive.xunit` at line 20), `Development.md`
- `docs/index.md` (packages), `docs/llms.txt` (packages)
- Package pages: `src/DbLive/README.md`, `src/DbLive.MSSQL/README.md`, `src/DbLive.PostgreSQL/README.md`, `src/DbLive.xunit/README.md`, new `src/DbLive.Testcontainers.MSSQL/README.md` and `src/DbLive.Testcontainers.PostgreSQL/README.md`

## Done when
- [x] Builders published with the other packages, the demos use them
- [x] Docs updated

## Notes
2026-11-03: split out of 0096-30 by decision of the maintainer: reduce the setup in the packages first, then build the template on top.

2026-11-03: decisions of the maintainer:
- Package: move `IDbLiveFixtureBuilder` to `DbLive`, builders in `DbLive.Testcontainers.*`, independent of xUnit. Rejected: `DbLive.xunit.MSSQL` / `DbLive.xunit.PostgreSQL`, which tie the builders to xUnit v2.
- Default PostgreSQL image: `postgres:latest`, not a pinned major version.
- Local server builders in the provider packages, after seeing the code (Design). Accepted trade-off: a type used only by tests in a package that production deployers reference; it adds no dependency, and the container builders are built on it.

2026-11-03, verified with Testcontainers 4.16.0 and Docker Desktop: the start pattern of the demo builders (`if (State != Running) await StartAsync()` on a static container), called from 8 tasks released at the same moment. All 8 saw the container not running and called `StartAsync`, and none failed. Testcontainers serialized the calls: one container was created and started; the other 7 calls ran the start and the readiness check (`pg_isready`) again on the running container. So the demos did not start two containers; they only repeated the start once per fixture that came early.

2026-11-03, verified: after the move, the three demo builders compile unchanged, with no warning (`using DbLive; using DbLive.xunit;`, namespaces outside `DbLive`). Code in a namespace under `DbLive` (the tests of this repository) finds the interface without a `using`. Only a fully qualified `DbLive.xunit.IDbLiveFixtureBuilder` breaks, so the task is not breaking.

2026-11-03, implementation decisions:
- The container builders are in the namespaces of the providers, `DbLive.MSSQL` and `DbLive.PostgreSQL`, not `DbLive.Testcontainers.MSSQL`. One `using` covers both builders of a provider. A namespace `DbLive.Testcontainers` would also hide the namespace `Testcontainers` of the library in code under `DbLive`: there `Testcontainers.MsSql.MsSqlBuilder` would resolve to `DbLive.Testcontainers`.
- The start of each image is cached as a `Task<string>` in a static dictionary, under a lock. Every fixture awaits the same start. A failed start (Docker not running, an image that does not exist) is not retried: every fixture of the run fails at once with the same exception, instead of each one waiting for a start of its own. Rejected: `Lazy<Task<T>>`, which the threading analyzers of the build reject (VSTHRD011 and VSTHRD003, verified); a `SemaphoreSlim` around the start, which retries a failed start once per fixture.
- The builders do not stop the container. `IDbLiveFixtureBuilder` has no end-of-run hook, and the demos did not stop it either. The Testcontainers resource reaper (Ryuk) removes it when the test process ends.
- `DbLiveBuilder` got an internal `GetRequiredService<T>()`; the `Create*` methods use it. The tests use it to read the connection string that a builder set (`IDbLiveDbConnection`).
- The demo test projects reference `DbLive.Testcontainers.*` and `DbLive.xunit` only, as the docs tell users to do; the provider comes through the Testcontainers package.
- The PostgreSQL container test takes the Chinook demo as the assembly with scripts and does not deploy it (600 KB of sample data). It creates and drops the database of each builder; the demos deploy through the builders.

## Result
- `IDbLiveFixtureBuilder` is in `src/DbLive/IDbLiveFixtureBuilder.cs`, namespace `DbLive`, with doc comments. `using DbLive.xunit;` is removed from the three test files that needed it only for the interface.
- `src/DbLive.MSSQL/MsSqlFixtureBuilder.cs`, `src/DbLive.PostgreSQL/PostgreSqlFixtureBuilder.cs`: as in Design. Tests: `src/DbLive.Tests/Builder/FixtureBuilderTests.cs`. Two builders from one fixture builder get different random names on the given server (host, port, user, password and other options kept), the provider is the right one, and the project is the given assembly.
- New projects and packages `src/DbLive.Testcontainers.MSSQL` (`MsSqlContainerFixtureBuilder`, depends on `DbLive.MSSQL` and `Testcontainers.MsSql`) and `src/DbLive.Testcontainers.PostgreSQL` (`PostgreSqlContainerFixtureBuilder`, depends on `DbLive.PostgreSQL` and `Testcontainers.PostgreSql`), with `DefaultImage` constants and package READMEs, in `src/DbLive.sln`. Tests against Docker: `MsSqlContainerFixtureBuilderTests` and `PostgreSqlContainerFixtureBuilderTests`. 4 builders started in parallel get one server (same host and port) and 4 databases, which are created and dropped.
- Demos: the three `Docker*FixtureBuilder.cs` files are deleted. The fixtures of `DemoMSSQL.Tests`, `AdventureWorks.Tests` and `Demo.PostgreSQL.Chinook.Tests` use the container builders; their projects reference `DbLive.Testcontainers.*` instead of `Testcontainers.*` and the provider.
- CI: `.github/workflows/cicd.yml` packs the two new packages into the local feed, and "Publish packages" pushes them with the others. `.github/scripts/package-tests.cs` has a new project, "Fixtures". It references `DbLive.xunit` and both Testcontainers packages from the feed and declares one-line fixtures with all four builders. Verified locally with the six packages packed from this branch: all checks pass. The nuspec of `DbLive.Testcontainers.MSSQL` depends on `DbLive.MSSQL` and `Testcontainers.MsSql` only.
- `dotnet test src/DbLive.sln --filter "Category!=LocalOnly"`: 505 passed, 0 failed (DbLive.Tests 397, DbLive.MSSQL.Tests 50, DbLive.PostgreSQL.Tests 37, DemoMSSQL.Tests 12, Demo.PostgreSQL.Chinook.Tests 5, AdventureWorks.Tests 4), with SQL Server and PostgreSQL in Docker. `mkdocs build --strict` was not run: there is no Python on this machine.
- Docs: listed under "Docs to update".
