# DbLive

**Database lifecycle management for .NET**: versioned migrations, re-runnable code objects, SQL unit tests and safe rollback in one workflow, for **SQL Server** and **PostgreSQL**.

DbLive treats the database as versioned, testable, deployable code. Your SQL lives in a .NET project, is reviewed like any other code, is tested in CI, and is deployed by a few lines of C#.

DbLive is built around one idea: the database is a **separate service** with its own interface, release cycle and tests, so it can be deployed without stopping the applications that use it. The model is optional: you can use DbLive as an ordinary migration tool and adopt the rest later. See [Database as a Service](database-as-service.md).

```csharp
IDbLive deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()                                  // or .PostgreSQL()
    .SetDbConnection(connectionString)
    .SetProject(Assembly.GetExecutingAssembly())
    .CreateDeployer();

await deployer.DeployAsync(DeployParameters.Default);
```

## What you get

| Area | What DbLive does |
|------|------------------|
| **Migrations** | Applies versioned scripts once per database, in order. Each version can have an **undo** script for rollback and a **breaking** script for the contract phase. |
| **Code** | Deploys procedures, functions, views and triggers from one file per object, and re-runs a file only when it changes. |
| **SQL tests** | Runs `.sql` tests in transactions that are always rolled back, as ordinary xUnit tests in Visual Studio and CI, against a temporary database built from your scripts (in Docker or on a local server). |
| **Zero downtime** | Destructive changes run in a separate **breaking** deployment, after the old application version is gone. |
| **Rollback** | Opt-in: deploying an older build with `AllowDatabaseDowngrade = true` reverts newer versions using the undo scripts stored in the database. Without it, an older build fails with `DowngradeNotAllowedException` and changes nothing. |
| **Safety** | A deployment stops before any of your scripts runs when an older build is deployed by mistake, an applied migration was edited (checksums, `Strict` by default) or the database belongs to another project (`ProjectId`). Breaking changes run only in a separate breaking deployment, and a deployment never runs SQL tests. |

## Where to start

- **[Getting Started](gettingstarted.md)**: create a project, add a migration, run the first deployment.
- **[Add a stored procedure and a unit test](unit-testing/stored-proc-and-unit-test.md)**: continue the tutorial with code and tests.
- **[Database as a Service](database-as-service.md)**: the idea behind DbLive, and the ways to use it, from classic migrations to an independently released database.
- **[Zero-Downtime Deployment](deployment/zero-downtime.md)**: the release runbook for 24/7 systems.

## Documentation map

| Section | Contents |
|---------|----------|
| [Project](project/index.md) | Project setup, [folder structure and file naming](project/structure.md), [`settings.json`](project/configuration.md) |
| [Deployment](deployment/index.md) | Pipeline and `DeployParameters`, [migrations](deployment/migrations.md), [code](deployment/code.md), [breaking changes](deployment/breaking-changes.md) |
| [Unit Testing](unit-testing/index.md) | Test files, assertions, `setup.test.sql`, xUnit integration |
| [Features](features/index.md) | ProjectId, checksums, downgrade and other features |
| [Examples](examples/index.md) | Demo projects and walkthroughs |
| [For AI agents](ai-agents.md) | `llms.txt` and the single-file reference for coding assistants |

## Packages

| Package | Purpose |
|---------|---------|
| `DbLive` | Core engine (installed automatically with a provider package) |
| `DbLive.MSSQL` | SQL Server and Azure SQL provider |
| `DbLive.PostgreSQL` | PostgreSQL provider |
| `DbLive.xunit` | SQL tests as xUnit tests, deployment tests |
| `DbLive.Testcontainers.MSSQL` | Test database: SQL Server in Docker, for the SQL tests |
| `DbLive.Testcontainers.PostgreSQL` | Test database: PostgreSQL in Docker, for the SQL tests |

Target frameworks: .NET 8, 9 and 10. Source code and issues: [github.com/zacoders/DbLive](https://github.com/zacoders/DbLive).

Every merge to `main` is published to NuGet. Breaking changes come only in a major version, with the upgrade steps in the [release notes](https://github.com/zacoders/DbLive/releases); see the [versioning policy](versioning.md). License: [MIT](https://github.com/zacoders/DbLive/blob/main/LICENSE).
