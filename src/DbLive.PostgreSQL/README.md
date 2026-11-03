# DbLive.PostgreSQL

PostgreSQL provider for [DbLive](https://github.com/zacoders/DbLive): versioned migrations, re-runnable code objects, SQL unit tests and zero-downtime breaking changes for .NET.

```csharp
using DbLive;
using DbLive.PostgreSQL;

IDbLive deployer = new DbLiveBuilder()
    .LogToConsole()
    .PostgreSQL()
    .SetDbConnection("Host=localhost;Database=mydb;Username=postgres;Password=...")
    .SetProject(Assembly.GetExecutingAssembly())
    .CreateDeployer();

await deployer.DeployAsync(DeployParameters.Default);
```

Each script file is sent as one multi-statement command. `Snapshot` isolation is not supported. DbLive's own SQL scripts (the `dblive` schema) are embedded in `DbLive.PostgreSQL.dll`; nothing needs to be copied next to your deployer.

For the SQL tests, `PostgreSqlFixtureBuilder` gives every test fixture (`DbLive.xunit`) a new database on your server; `DbLive.Testcontainers.PostgreSQL` runs the server in Docker instead.

- Documentation: https://zacoders.github.io/DbLive/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
