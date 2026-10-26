# Project Configuration

Project settings live in `Scripts/settings.json`. The file is optional; without it every setting has its default value.

```json
{
  // Optional. Binds this project to a database; max 128 characters.
  "ProjectId": "billing",

  /* Deployment | Migration | None */
  "TransactionWrapLevel": "Migration",

  /* ReadCommitted | RepeatableRead | Serializable | Snapshot (SQL Server only) */
  "TransactionIsolationLevel": "ReadCommitted",

  "EnforceTimestampVersion": true,

  "CodeSubFoldersDeploymentOrder": [ "schemas", "types" ]
}
```

## File format

- Comments (`//` and `/* */`) are allowed.
- **Property names are case-sensitive**: `TransactionWrapLevel`, not `transactionWrapLevel`.
- **Unknown properties are rejected**, so a typo fails the deployment instead of being silently ignored.
- **Trailing commas are not allowed.**
- Enum values are case-insensitive: `"none"` and `"None"` both work.
- Time spans use `"hh:mm:ss"` or `"d.hh:mm:ss"`, for example `"01:00:00"` or `"1.00:00:00"`.
- A list replaces the default list; it is not merged with it.

## Settings

### General

| Setting | Default | Description |
|---------|---------|-------------|
| `ProjectId` | none | Binds the project to the target database on the first deployment and blocks deployment to a database bound to another project. See [ProjectId](../features/project-id.md). |
| `EnforceTimestampVersion` | `false` | Require migration versions in `yyyyMMddHHmmss` format (14 digits, valid date, year from 2000 up to next month). |
| `MigrationChecksumMode` | `Strict` | `Strict` stops the deployment when an applied migration file was changed; `Warn` logs a warning and continues. See [Checksums](../deployment/migrations.md#checksums). |

### Folders

| Setting | Default |
|---------|---------|
| `MigrationsFolder` | `Migrations` |
| `CodeFolder` | `Code` |
| `TestsFolder` | `Tests` |
| `BeforeDeployFolder` | `BeforeDeploy` |
| `AfterDeployFolder` | `AfterDeploy` |
| `TestFilePatterns` | `["test.*.sql", "t.*.sql", "*.test.sql", "*.t.sql"]`: which files in `Code` are tests |
| `CodeSubFoldersDeploymentOrder` | `[]`: `Code` subfolders deployed first, in the listed order, before all other code |

Entries in `CodeSubFoldersDeploymentOrder` are compared using the operating system's path separator. A nested entry such as `"dbo\\functions"` works only on Windows; prefer top-level folder names if you deploy from Linux too.

### Transactions

| Setting | Default | Description |
|---------|---------|-------------|
| `TransactionWrapLevel` | `Migration` | `Migration`: each migration in its own transaction. `Deployment`: the whole deployment in one transaction. `None`: no transactions. |
| `TransactionIsolationLevel` | `ReadCommitted` | Isolation level of deployment transactions and commands. `Snapshot` is SQL Server only and requires `ALLOW_SNAPSHOT_ISOLATION ON`. |
| `TestsTransactionIsolationLevel` | `Serializable` | Isolation level of the transaction each SQL test runs in. |

### Parallelism

| Setting | Default | Description |
|---------|---------|-------------|
| `NumberOfThreadsForCodeDeploy` | `10` | Code files deployed in parallel. Forced to 1 with `TransactionWrapLevel: Deployment`. |
| `MaxCodeDeployRetries` | `5` | Attempts per code file before the deployment fails. |
| `NumberOfThreadsForTestsRun` | `10` | SQL tests run in parallel at the end of a deployment. |

### Timeouts

| Setting | Default | Applies to |
|---------|---------|------------|
| `MigrationTimeout` | `12:00:00` | Each migration, breaking and undo script; also the transaction of a migration and of the breaking step |
| `DowngradeTimeout` | `12:00:00` | The transaction of a downgrade |
| `DeploymentTimeout` | `1.00:00:00` | The transaction of the whole deployment (`TransactionWrapLevel: Deployment`) |
| `CodeItemTimeout` | `00:00:30` | Each code file |
| `UnitTestItemTimeout` | `00:01:00` | Each SQL test, including its transaction |
| `BeforeDeployFolderTimeout` | `06:00:00` | Each BeforeDeploy script |
| `AfterDeployFolderTimeout` | `06:00:00` | Each AfterDeploy script |

!!! warning "Transactions longer than 10 minutes"
    DbLive uses `System.Transactions`. .NET caps every transaction at `TransactionManager.MaximumTimeout`, which is **10 minutes** by default, whatever the timeout settings above say. A transactional migration, breaking step or `Deployment`-level deployment that runs longer is aborted. Either run long migrations without a transaction (see below), or raise the cap in your deployer before calling `DeployAsync` (.NET 7 and later):

    ```csharp
    System.Transactions.TransactionManager.MaximumTimeout = TimeSpan.FromHours(12);
    ```

## Choosing a transaction level

DbLive does not wrap a deployment in one transaction by default, and that is intentional (see [Philosophy](../philosophy.md)):

- **`Migration`** (default): each migration version commits or rolls back on its own, together with its bookkeeping. If version 5 fails, versions 1 to 4 stay applied and the next deployment continues with version 5. The database stays usable between migrations, which is what a 24/7 system needs.
- **`Deployment`**: all or nothing. Simple for small databases and test environments, but it holds locks for the whole deployment and runs code deployment single-threaded.
- **`None`**: nothing is transactional. Use it per migration rather than for the whole project.

Undo scripts of a downgrade always run in one transaction, and each SQL test always runs in its own transaction that is rolled back.

## Per-migration settings

A file named `<version>.settings.json` (or `<version>.s.json`) next to a migration overrides settings for that version only. It follows the same format rules and accepts three keys:

| Key | Effect |
|-----|--------|
| `TransactionWrapLevel` | `None` runs the migration without a transaction; `Migration` wraps it. |
| `TransactionIsolationLevel` | Isolation level of this migration's transaction. |
| `MigrationTimeout` | Timeout of this migration's **transaction**. The command timeout still comes from the project-level `MigrationTimeout`, and without a transaction this key has no effect. |

Typical use: build an index online without holding a long transaction on a busy table.

```json
{
  "TransactionWrapLevel": "None", // online index creation
  "TransactionIsolationLevel": "ReadCommitted"
}
```

Some statements cannot run inside a transaction at all: `ALTER DATABASE` and full-text DDL on SQL Server, `CREATE INDEX CONCURRENTLY` and `VACUUM` on PostgreSQL. Give them their own migration version with `"TransactionWrapLevel": "None"`. On PostgreSQL keep such a statement alone in its file, because a file with several statements runs as one implicit transaction.

Without a transaction a failed migration may be half-applied, so write such migrations and their undo scripts to be re-runnable (`if not exists`, `drop ... if exists`).

Per-migration settings are not used for breaking scripts.
