---
type: feature
priority: low
created: 2026-11-03
---
# Ready-made fixture builders: local server and Testcontainers

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
- `IDbLiveFixtureBuilder` moves from `DbLive.xunit` to `DbLive`, namespace `DbLive`. A builder class written by a user probably keeps compiling: it already has `using DbLive;` for `DbLiveBuilder`, and the `DbLive.xunit` namespace still exists (to verify). The binary break does not matter, because all packages are released together with one version.
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
  - The start is thread safe. The demo builders check `State != Running` and then call `StartAsync`, so two fixtures that xUnit initializes in parallel can both start the container (to verify).

## Where
- `src/DbLive.xunit/IDbLiveFixtureBuilder.cs`, moves to `src/DbLive/`. Used by `src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs`, `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs`, and the test builders in `src/DbLive.Tests/SqlTest/SqlFactDiscovererTests.cs` and `src/DbLive.Tests/Deploy/DeploymentTestFixtureTests.cs`.
- Random database names: `src/DbLive.MSSQL/MSSQLConnectionStringExtensions.cs`, `src/DbLive.PostgreSQL/PostgreSqlConnectionStringExtensions.cs`.
- Copies in the demos, to be replaced: `src/Demo/DemoMSSQL.Tests/DockerMsSqlFixtureBuilder.cs`, `src/Demo/AdventureWorks/AdventureWorks.Tests/DockerMsSqlFixtureBuilder.cs`, `src/Demo/Demo.PostgreSQL.Chinook.Tests/DockerPostgresFixtureBuilder.cs`.
- `src/Directory.Packages.props:28-29`: Testcontainers versions.
- `.github/workflows/cicd.yml:73-83`: pack the new packages with the others.
- "The four packages" in `docs/versioning.md:3`, `Development.md:7`, `docs/llms-full.txt` §2.

## Plan
- [ ] Move `IDbLiveFixtureBuilder` to `DbLive`.
- [ ] `MsSqlFixtureBuilder` and `PostgreSqlFixtureBuilder` in the provider packages, with unit tests: each `GetBuilderAsync` gets a new database name on the given server, and the project is the given assembly.
- [ ] `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL` with a thread-safe container start, tested against Docker in `DbLive.MSSQL.Tests` and `DbLive.PostgreSQL.Tests`.
- [ ] Demos use them.
- [ ] Packed and published with the other packages.
- [ ] Docs.

## Docs to update
- `README.md` ("Quick start", step 1: packages)
- `docs/unit-testing/index.md` ("Running tests in Visual Studio and CI (xUnit)", including "Without Docker, point the builder at a local server"), `docs/unit-testing/stored-proc-and-unit-test.md`
- `docs/llms-full.txt` §2, §11 (C# API), §13, §18.1
- `docs/versioning.md` (list of packages; `IDbLiveFixtureBuilder` is listed under `DbLive.xunit` at line 20), `Development.md`

## Done when
- [ ] Builders published with the other packages, the demos use them
- [ ] Docs updated

## Notes
2026-11-03: split out of 0096-30 by decision of the maintainer: reduce the setup in the packages first, then build the template on top.

2026-11-03: decisions of the maintainer:
- Package: move `IDbLiveFixtureBuilder` to `DbLive`, builders in `DbLive.Testcontainers.*`, independent of xUnit. Rejected: `DbLive.xunit.MSSQL` / `DbLive.xunit.PostgreSQL`, which tie the builders to xUnit v2.
- Default PostgreSQL image: `postgres:latest`, not a pinned major version.
- Local server builders in the provider packages, after seeing the code (Design). Accepted trade-off: a type used only by tests in a package that production deployers reference; it adds no dependency, and the container builders are built on it.
