# DbLive.MSSQL

SQL Server and Azure SQL provider for [DbLive](https://github.com/zacoders/DbLive): versioned migrations, re-runnable code objects, SQL unit tests and zero-downtime breaking changes for .NET.

```csharp
using DbLive;
using DbLive.MSSQL;

IDbLive deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()
    .SetDbConnection("Server=localhost;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;")
    .SetProject(Assembly.GetExecutingAssembly())
    .CreateDeployer();

await deployer.DeployAsync(DeployParameters.Default);
```

Scripts may use `GO` batch separators, except SQL test files. DbLive's own SQL scripts (the `dblive` schema) are embedded in `DbLive.MSSQL.dll`; nothing needs to be copied next to your deployer.

For the SQL tests, `MsSqlFixtureBuilder` gives every test fixture (`DbLive.xunit`) a new database on your server; `DbLive.Testcontainers.MSSQL` runs the server in Docker instead.

- Documentation: https://zacoders.github.io/DbLive/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
