# DbLive

Database lifecycle management for .NET: versioned SQL migrations, re-runnable code objects (procedures, functions, views), SQL unit tests, and a separate breaking-changes phase for zero-downtime releases.

This is the core package. Install it through a database provider:

- `DbLive.MSSQL`: SQL Server and Azure SQL
- `DbLive.PostgreSQL`: PostgreSQL
- `DbLive.xunit`: run SQL tests as xUnit tests
- `DbLive.Testcontainers.MSSQL`, `DbLive.Testcontainers.PostgreSQL`: a test database in Docker for the SQL tests

It embeds the `Scripts` folder of every project that references it, also through these packages, in the project's assembly; there is nothing to add to the `.csproj`. A project with only SQL scripts references `DbLive` itself.

```csharp
IDbLive deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()                                  // or .PostgreSQL()
    .SetDbConnection(connectionString)
    .SetProject(Assembly.GetExecutingAssembly())  // project that embeds its Scripts folder
    .CreateDeployer();

await deployer.DeployAsync(DeployParameters.Default);
```

- Documentation: https://zacoders.github.io/DbLive/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Source and demos: https://github.com/zacoders/DbLive
- Release notes with the upgrade steps: https://github.com/zacoders/DbLive/releases (versioning policy: https://zacoders.github.io/DbLive/versioning/)
- License: MIT, https://github.com/zacoders/DbLive/blob/main/LICENSE
