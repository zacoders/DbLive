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

Scripts may use `GO` batch separators, except SQL test files. The package copies its internal SQL scripts to `DbLive.MSSQL/` in the build and publish output; keep that folder next to your deployer.

- Documentation: https://zacoders.github.io/DbLive/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
