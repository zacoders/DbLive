# Deployment

A deployment is one call to `IDbLive.DeployAsync(DeployParameters)`. DbLive has no command-line tool; you start deployments from your own code: a small console app (the *deployer*), a test, or a step of your application's tooling.

- [Migrations](migrations.md): versions, checksums, rolling back a release
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
    : DeployParameters.Default);
```

It reads the scripts embedded in the SQL project assembly, so it deploys exactly what was built and can be started from any working directory (see [How DbLive finds the scripts](../project/index.md#how-dblive-finds-the-scripts)). A failed deployment throws, so the process exits with a non-zero code and your CI step fails. It does not run the SQL tests: they run in CI (see [SQL tests](#sql-tests)).

`DeployParameters.Default` does not create the database: create it before the first deployment, or, for a local or test database, deploy with `DeployParameters.Default with { CreateDbIfNotExists = true }`. The settings of a production deployment are collected in [Production](#production).

## What a deployment does

Every `DeployAsync` call runs these steps in this order:

1. **Create the database** if it does not exist and `CreateDbIfNotExists` is on. It is off by default: then a missing database fails the deployment with `DatabaseNotFoundException`, which names the database and the server.
2. **Self-deploy**: create or upgrade DbLive's own `dblive` schema. A database that a newer DbLive changed in a way this version cannot read stops the deployment here, before any change (see [The DbLive version a database requires](#the-dblive-version-a-database-requires)).
3. **Validate**: [ProjectId](../features/project-id.md), isolation levels, that every migration version applied to the database is in the project (otherwise `DatabaseAheadOfProjectException`, see [Which migrations run](migrations.md#which-migrations-run)), and the [checksums](migrations.md#checksums) of applied migrations. The applied versions check is the first read of the project files, so a file outside the project folders or a wrong migration file name stops the deployment here, before any of your scripts runs.
4. **Record** all migration files of the project in `dblive.migration`.
5. **BeforeDeploy** scripts.
6. **Migrations**: pending versions in ascending order (`DeployMigrations`).
7. **Code**: new and changed code files (`DeployCode`).
8. **Breaking** (`DeployBreaking`): fails unless every code file is deployed with its current content; then every breaking script of the Code folder, then the pending breaking scripts of migrations (see [Breaking Changes](breaking-changes.md#when-breaking-scripts-run)).
9. **AfterDeploy** scripts.

With `TransactionWrapLevel: Deployment`, steps 3 (from the applied versions check) to 9 run in one transaction. The SQL tests are not a step of the deployment (see [SQL tests](#sql-tests)).

Steps 2 to 5 and 9 run whatever the flags say. A breaking-only deployment therefore still validates, records migrations and runs BeforeDeploy and AfterDeploy.

## DeployParameters

| Property | Default | Meaning |
|----------|---------|---------|
| `CreateDbIfNotExists` | `false` | Create the database if missing. SQL Server connects to `master`, PostgreSQL to `postgres`; the login needs permission to create databases. Off by default, so that a typo in the database name fails the deployment instead of creating an empty database; turn it on for local and test databases. |
| `DeployMigrations` | `true` | Apply pending migrations. |
| `DeployCode` | `true` | Deploy new and changed code files. |
| `DeployBreaking` | `false` | Run the breaking phase: the breaking scripts of the Code folder and the pending breaking scripts of migrations. |
| `RepairMigrationChecksums` | `false` | Accept changed applied migration files by updating their stored checksums. |

### Presets

| Preset | Create DB | Migrations | Code | Breaking |
|--------|-----------|------------|------|----------|
| `DeployParameters.Default` | no | yes | yes | no |
| `DeployParameters.Breaking` | no | no | no | yes |

`DeployParameters` is a record, so adjust a preset with `with`:

```csharp
await deployer.DeployAsync(DeployParameters.Default with { CreateDbIfNotExists = true });
```

## SQL tests

A deployment does not run the SQL tests, so a production deployer cannot run them by mistake. Run them in CI with `dotnet test`, against a disposable database: `[SqlFact]` runs each test, and the [deployment test](../unit-testing/deployment-tests.md) deploys the project as production does and runs all tests after the release and after the breaking phase. See [Unit Testing](../unit-testing/index.md), and [Running all tests without xUnit](../unit-testing/index.md#running-all-tests-without-xunit) for other test frameworks.

!!! tip "Tests and production"
    SQL tests roll back everything they change, but by default they run with `Serializable` isolation and take locks on the tables they read. Do not run them against production.

## Logging

DbLive logs through Serilog. Without configuration nothing is logged.

```csharp
builder.LogToConsole();                                   // console
builder.LogToXUnitOutput(testOutputHelper);               // xUnit test output (DbLive.xunit)
builder.ConfigureServices(s => s.AddSingleton<Serilog.ILogger>(myLogger));   // any Serilog logger
```

## A typical pipeline

1. **Build** the solution.
2. **Test**: `dotnet test` deploys the database into a Docker container (Testcontainers) as production does, runs every breaking script twice and all SQL tests. See [Unit Testing](../unit-testing/index.md).
3. **Deploy the database**: run the deployer with `Default`.
4. **Deploy the application**. Old and new versions run side by side.
5. **Remove the old application version.**
6. **Deploy breaking changes**: run the deployer with `Breaking`, once the release is confirmed.

Steps 3 to 6 are explained in [Zero-Downtime Deployment](zero-downtime.md). Until step 6 a release is rolled back by rolling back the application; the database stays (see [Roll back a release](migrations.md#roll-back-a-release)).

## Production

The settings of a deployment to a production database, in one place. The defaults are the safe values, except `ProjectId`, which only you can choose.

| Setting | In production | Why |
|---------|---------------|-----|
| `ProjectId` in `settings.json` | Set, unique per project | Stops a deployment into the database of another project. Without it every deployment logs a warning. See [ProjectId](../features/project-id.md). |
| `CreateDbIfNotExists` | `false` (default) | Create the production database yourself. A typo in the database name then fails the deployment instead of creating an empty database and deploying the whole schema into it, and the deployer's login needs no permission to create databases. |
| `RepairMigrationChecksums` | `false` (default); `true` only for one deployment, after you checked that the database matches the files | Left on, it accepts every edited applied migration without a check. See [Checksums](migrations.md#checksums). |
| `MigrationChecksumMode` | `Strict` (default) | `Warn` deploys although an applied migration was edited, so the files no longer describe the database. |
| `TransactionWrapLevel` | `Migration` (default); `None` in the [item settings](../project/configuration.md#item-settings) of a script that cannot run in a transaction | `Deployment` holds locks for the whole deployment and deploys code single-threaded: it is for small databases and test environments. See [Choosing a transaction level](../project/configuration.md#choosing-a-transaction-level). |
| `MigrationTimeout`, `CodeItemTimeout` | What your release window allows; `Timeout` in the item settings of a longer migration script | A script that runs longer fails, and the deployment with it. Defaults: 12 hours per migration script, 30 seconds per code file. See [Timeouts](../project/configuration.md#timeouts). |
| SQL tests | Not in the deployer | They run in CI against a disposable database. See [SQL tests](#sql-tests). |

## Bookkeeping tables

DbLive keeps its state in the `dblive` schema of the target database. Do not create your own objects in that schema.

| Table | Content |
|-------|---------|
| `dblive.version` | DbLive's own schema version, the bound `project_id`, and the oldest DbLive version that can deploy the database (`min_dblive_lib_version`) |
| `dblive.migration` | One row per migration file: version, type, status (`none`, `applied`), checksum, applied time |
| `dblive.code` | One row per code file: status, checksum, last error |
| `dblive.unit_test_run` | Last result of each SQL test |
| `dblive.folder_items` | Last run of each BeforeDeploy and AfterDeploy script |

On SQL Server the schema also has the table type `dblive.tvp_unique_string_list`: DbLive passes lists of file paths to its queries in it.

Files are identified by their path relative to the scripts root, stored in lower case with `/` (`code/dbo/getuser.sql`).

### The DbLive version a database requires

A newer DbLive can store in these tables what an older one cannot read. So the database keeps the oldest DbLive version that can deploy it, in `dblive.version.min_dblive_lib_version`. Every deployment raises it to the minimum of its DbLive version and never lowers it. A deployment or `RunAllTestsAsync()` with an older DbLive fails with `DatabaseRequiresNewerDbLiveException` before it changes anything; the message names the version to update the DbLive packages to.

A release of DbLive after which older versions can no longer deploy the database says so in its [release notes](https://github.com/zacoders/DbLive/releases). If you roll back a release that upgraded to such a version, roll back the application and keep the database (see [Roll back a release](migrations.md#roll-back-a-release)): the previous build of the database fails with this exception. DbLive versions before 2.0.0 do not check the required version: do not deploy a database that DbLive 2.0 or later has deployed with them.

```sql
select version, item_type, status, applied_utc, relative_path
from dblive.migration
order by version, item_type;
```
