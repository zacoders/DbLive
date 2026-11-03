# DbLive.Testcontainers.PostgreSQL

Runs the SQL tests of a [DbLive](https://github.com/zacoders/DbLive) project against PostgreSQL in Docker. `PostgreSqlContainerFixtureBuilder` starts one container per test run with [Testcontainers](https://dotnet.testcontainers.org/) and gives every test fixture a new database with a random name. It brings `DbLive.PostgreSQL` and `Testcontainers.PostgreSql`; Docker must be running.

```csharp
using DbLive.PostgreSQL;
using DbLive.xunit.SqlTest;
using System.Reflection;

public class MyDbTestFixture()
    : DbLiveTestFixture(new PostgreSqlContainerFixtureBuilder(Assembly.Load("MyDb")), dropDatabaseOnComplete: true);
```

- The image is `postgres:latest` unless you pass another one: `new PostgreSqlContainerFixtureBuilder(project, "postgres:17")`.
- Fixtures that start at the same time share one container; Testcontainers removes it when the test run ends.
- Without Docker, use `PostgreSqlFixtureBuilder` from `DbLive.PostgreSQL` with a connection string to your server.

- Unit testing guide: https://zacoders.github.io/DbLive/unit-testing/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
