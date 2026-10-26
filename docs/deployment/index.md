# Deployment

A deployment is one call to `IDbLive.DeployAsync(DeployParameters)`. DbLive has no command-line tool; you start deployments from your own code: a small console app (the *deployer*), a test, or a step of your application's tooling.

- [Migrations](migrations.md): versions, undo scripts, downgrade, checksums
- [Code Deployment](code.md): procedures, functions, views
- [Breaking Changes](breaking-changes.md): the contract phase
- [Zero-Downtime Deployment](zero-downtime.md): the release runbook

## A deployer

```csharp
using DbLive;
using DbLive.MSSQL;
using System.Reflection;

string connectionString = Environment.GetEnvironmentVariable("MYDB_CONNECTION_STRING")
    ?? throw new InvalidOperationException("MYDB_CONNECTION_STRING is not set.");

bool breaking = args.Contains("--breaking");

IDbLive deployer = new DbLiveBuilder()
    .LogToConsole()
    .SqlServer()                                   // or .PostgreSQL()
    .SetDbConnection(connectionString)
    .SetProject(Assembly.GetExecutingAssembly())
    .CreateDeployer();

await deployer.DeployAsync(breaking
    ? DeployParameters.Breaking
    : DeployParameters.Default with { RunTests = false });
```

Start it from its output folder, or see [How DbLive finds the scripts](../project/index.md#how-dblive-finds-the-scripts) for a version that works from any working directory. A failed deployment throws, so the process exits with a non-zero code and your CI step fails.

## What a deployment does

Every `DeployAsync` call runs these steps in this order:

1. **Create the database** if it does not exist (`CreateDbIfNotExists`).
2. **Self-deploy**: create or upgrade DbLive's own `dblive` schema.
3. **Validate**: [ProjectId](../features/project-id.md), isolation levels, and the [checksums](migrations.md#checksums) of applied migrations.
4. **Record** all migration files of the project in `dblive.migration`.
5. **Downgrade** if the database has versions this project does not contain (see [Downgrade](migrations.md#downgrade)).
6. **BeforeDeploy** scripts.
7. **Migrations**: pending versions in ascending order (`DeployMigrations`).
8. **Code**: new and changed code files (`DeployCode`).
9. **Breaking**: pending breaking scripts (`DeployBreaking`).
10. **AfterDeploy** scripts.
11. **Tests**: all SQL tests (`RunTests`). If any test fails, `DeployAsync` throws after all tests have run.

With `TransactionWrapLevel: Deployment`, steps 3 (checksums) to 10 run in one transaction. Tests always run after the deployment has been committed.

Steps 1 to 6 and 10 run whatever the flags say. A breaking-only deployment therefore still validates, records migrations, checks for a downgrade and runs BeforeDeploy and AfterDeploy.

## DeployParameters

| Property | Default | Meaning |
|----------|---------|---------|
| `CreateDbIfNotExists` | `true` | Create the database if missing. SQL Server connects to `master`, PostgreSQL to `postgres`; the login needs permission to create databases. |
| `DeployMigrations` | `true` | Apply pending migrations. |
| `DeployCode` | `true` | Deploy new and changed code files. |
| `DeployBreaking` | `false` | Apply pending breaking scripts. |
| `RunTests` | `true` | Run all SQL tests after the deployment. |
| `AllowDatabaseDowngrade` | `false` | Allow reverting versions that are applied in the database but missing from the project. |
| `RepairMigrationChecksums` | `false` | Accept changed applied migration files by updating their stored checksums. |

### Presets

| Preset | Create DB | Migrations | Code | Breaking | Tests |
|--------|-----------|------------|------|----------|-------|
| `DeployParameters.Default` | yes | yes | yes | no | yes |
| `DeployParameters.Breaking` | no | no | no | yes | no |
| `DeployParameters.BreakingAndTests` | no | no | no | yes | yes |

`DeployParameters` is a record, so adjust a preset with `with`:

```csharp
await deployer.DeployAsync(DeployParameters.Default with { RunTests = false });
```

!!! tip "Tests and production"
    SQL tests roll back everything they change, but by default they run with `Serializable` isolation and take locks on the tables they read. Turn them off for production deployments (`RunTests = false`) and run them in CI against a disposable database instead.

## Logging

DbLive logs through Serilog. Without configuration nothing is logged.

```csharp
builder.LogToConsole();                                   // console
builder.LogToXUnitOutput(testOutputHelper);               // xUnit test output (DbLive.xunit)
builder.ConfigureServices(s => s.AddSingleton<Serilog.ILogger>(myLogger));   // any Serilog logger
```

## A typical pipeline

1. **Build** the solution.
2. **Test**: `dotnet test` deploys the database into a Docker container (Testcontainers), runs the undo round-trips and all SQL tests. See [Unit Testing](../unit-testing/index.md).
3. **Deploy the database**: run the deployer with `Default` (without tests).
4. **Deploy the application**. Old and new versions run side by side.
5. **Remove the old application version.**
6. **Deploy breaking changes**: run the deployer with `Breaking`.

Steps 3 to 6 are explained in [Zero-Downtime Deployment](zero-downtime.md).

## Bookkeeping tables

DbLive keeps its state in the `dblive` schema of the target database. Do not create your own objects in that schema.

| Table | Content |
|-------|---------|
| `dblive.version` | DbLive's own schema version and the bound `project_id` |
| `dblive.migration` | One row per migration file: version, type, status (`none`, `applied`, `reverted`), checksum, applied time; the content of undo scripts |
| `dblive.code` | One row per code file: status, checksum, last error |
| `dblive.unit_test_run` | Last result of each SQL test |
| `dblive.folder_items` | Last run of each BeforeDeploy and AfterDeploy script |

```sql
select version, item_type, status, applied_utc, relative_path
from dblive.migration
order by version, item_type;
```
