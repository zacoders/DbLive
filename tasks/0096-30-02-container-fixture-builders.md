---
type: feature
priority: low
created: 2026-11-03
---
# Ready-made Testcontainers fixture builders

## Goal
Every test project writes its own `IDbLiveFixtureBuilder` (`docs/unit-testing/index.md` "A fixture builder", `docs/llms-full.txt` §13.1). It is about 25 lines: start a container once per test run, give each fixture a database with a random name, call the provider and `SetProject`. The three demos have copies of it. With a builder in a package, the fixture is one line, and Testcontainers comes in as a dependency instead of a package reference the user adds by hand:

```csharp
public class MyDbTestFixture()
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
```

## Design
- `MsSqlContainerFixtureBuilder` and `PostgreSqlContainerFixtureBuilder`: the SQL project assembly, and an optional image. Defaults are the images of the demos: `mcr.microsoft.com/mssql/server:2025-latest`, `postgres:latest`.
- One container per image per test run, started on first use. Each `GetBuilderAsync` returns a builder with a new random database name.
- The start is thread safe. The demo builders check `State != Running` and then call `StartAsync`, so two fixtures that xUnit initializes in parallel can both start the container (to verify).

- [ ] Which package? It cannot be `DbLive.xunit`: Testcontainers would become a dependency of every test project, and a SQL Server project would pull Npgsql. Options: (a) one package per provider, `DbLive.xunit.MSSQL` and `DbLive.xunit.PostgreSQL`. (b) Move `IDbLiveFixtureBuilder` to `DbLive`; then the builders do not depend on xUnit, can be `DbLive.Testcontainers.MSSQL` / `.PostgreSQL`, and also serve a future xUnit v3 package. (b) moves a public type: breaking.
- [ ] Also a builder for a local server without Docker: a connection string plus a random database name? It needs no Testcontainers. It could live in `DbLive.xunit` if the provider call (`b => b.SqlServer()`) is passed in.
- [ ] `postgres:latest` as the default, or a pinned major version, so that the tests of a user do not change when a new PostgreSQL is released?

## Where
- `src/DbLive.xunit/IDbLiveFixtureBuilder.cs`.
- Copies in the demos, to be replaced: `src/Demo/DemoMSSQL.Tests/DockerMsSqlFixtureBuilder.cs`, `src/Demo/AdventureWorks/AdventureWorks.Tests/DockerMsSqlFixtureBuilder.cs`, `src/Demo/Demo.PostgreSQL.Chinook.Tests/DockerPostgresFixtureBuilder.cs`.
- `src/Directory.Packages.props:28-29`: Testcontainers versions.
- `.github/workflows/cicd.yml:73-83`: pack the new packages with the others.
- "The four packages" in `docs/versioning.md:3`, `Development.md:7`, `docs/llms-full.txt` §2.

## Plan
- [ ] Answer the open questions.
- [ ] Builders with a thread-safe container start, tested against Docker in `DbLive.MSSQL.Tests` and `DbLive.PostgreSQL.Tests`.
- [ ] Demos use them.
- [ ] Packed and published with the other packages.
- [ ] Docs.

## Docs to update
- `README.md` ("Quick start", step 1: packages)
- `docs/unit-testing/index.md` ("Running tests in Visual Studio and CI (xUnit)"), `docs/unit-testing/stored-proc-and-unit-test.md`
- `docs/llms-full.txt` §2, §13, §18.1
- `docs/versioning.md` (list of packages and their public API), `Development.md`

## Done when
- [ ] Builders published with the other packages, the demos use them
- [ ] Docs updated

## Notes
2026-11-03: split out of 0096-30 by decision of the maintainer: reduce the setup in the packages first, then build the template on top.
