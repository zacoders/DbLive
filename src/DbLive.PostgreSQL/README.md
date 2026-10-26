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

Each script file is sent as one multi-statement command. `Snapshot` isolation is not supported. The package copies its internal SQL scripts to `DbLive.PostgreSQL/` in the build and publish output; keep that folder next to your deployer.

- Documentation: https://zacoders.github.io/DbLive/
- Complete reference for AI coding agents: https://zacoders.github.io/DbLive/llms-full.txt
