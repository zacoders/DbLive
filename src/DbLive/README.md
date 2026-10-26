# DbLive

Database lifecycle management for .NET: versioned SQL migrations with undo scripts, re-runnable code objects (procedures, functions, views), SQL unit tests, and a separate breaking-changes phase for zero-downtime releases.

This is the core package. Install it through a database provider:

- `DbLive.MSSQL`: SQL Server and Azure SQL
- `DbLive.PostgreSQL`: PostgreSQL
- `DbLive.xunit`: run SQL tests as xUnit tests

```csharp
IDbLive deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()                                  // or .PostgreSQL()
    .SetDbConnection(connectionString)
    .SetProject(Assembly.GetExecutingAssembly())  // project with a Scripts folder
    .CreateDeployer();

await deployer.DeployAsync(DeployParameters.Default);
```

- Documentation: https://zacoders.github.io/DbLive/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
- Source and demos: https://github.com/zacoders/DbLive
