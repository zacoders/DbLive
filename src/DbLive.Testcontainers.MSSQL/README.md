# DbLive.Testcontainers.MSSQL

Runs the SQL tests of a [DbLive](https://github.com/zacoders/DbLive) project against SQL Server in Docker. `MsSqlContainerFixtureBuilder` starts one container per test run with [Testcontainers](https://dotnet.testcontainers.org/) and gives every test fixture a new database with a random name. It brings `DbLive.MSSQL` and `Testcontainers.MsSql`; Docker must be running.

```csharp
using DbLive.MSSQL;
using DbLive.xunit.SqlTest;
using System.Reflection;

public class MyDbTestFixture()
    : DbLiveTestFixture(new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
```

- The image is `mcr.microsoft.com/mssql/server:2025-latest` unless you pass another one: `new MsSqlContainerFixtureBuilder(project, "mcr.microsoft.com/mssql/server:2022-latest")`.
- Fixtures that start at the same time share one container; Testcontainers removes it when the test run ends.
- Without Docker, use `MsSqlFixtureBuilder` from `DbLive.MSSQL` with a connection string to your server.

- Unit testing guide: https://zacoders.github.io/DbLive/unit-testing/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
