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
| `TransactionWrapLevel` | `Migration` | `Migration`: each migration, breaking and undo script in its own transaction. `Deployment`: the whole deployment in one transaction. `None`: no transactions. |
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
| `MigrationTimeout` | `12:00:00` | Each migration, breaking and undo script: its command and its transaction. An [item settings file](#item-settings) can override it with `Timeout`. |
| `DeploymentTimeout` | `1.00:00:00` | The transaction of the whole deployment (`TransactionWrapLevel: Deployment`) |
| `CodeItemTimeout` | `00:00:30` | Each code file |
| `UnitTestItemTimeout` | `00:01:00` | Each SQL test, including its transaction |
| `BeforeDeployFolderTimeout` | `06:00:00` | Each BeforeDeploy script |
| `AfterDeployFolderTimeout` | `06:00:00` | Each AfterDeploy script |

`DowngradeTimeout` no longer exists: an undo script runs with `MigrationTimeout`, or with the `Timeout` of its `<version>.undo.settings.json`. A `settings.json` that still has the key fails as an unknown property.

!!! note "Transactions longer than 10 minutes"
    DbLive uses `System.Transactions`. .NET caps every transaction at the process-wide `TransactionManager.MaximumTimeout`, which is 10 minutes by default. Before opening a transaction, DbLive raises that cap to the transaction's timeout and never lowers it, so the settings above apply as written. Other `TransactionScope`s in the same process are afterwards capped at the largest timeout DbLive used.

## Choosing a transaction level

DbLive does not wrap a deployment in one transaction by default, and that is intentional (see [Database as a Service](../database-as-service.md#transactions)):

- **`Migration`** (default): each migration, breaking and undo script commits or rolls back on its own, together with its bookkeeping. If version 5 fails, versions 1 to 4 stay applied and the next deployment continues with version 5. A downgrade works the same way: if the undo of version 3 fails, the undo of version 4 stays committed and the next downgrade continues with version 3. The database stays consistent between scripts, which is what a 24/7 system needs.
- **`Deployment`**: all or nothing, including downgrades and breaking scripts. Simple for small databases and test environments, but it holds locks for the whole deployment and runs code deployment single-threaded.
- **`None`**: nothing is transactional. Use it for single scripts through an [item settings file](#item-settings) rather than for the whole project.

Each SQL test always runs in its own transaction that is rolled back.

## Item settings

Each migration, breaking and undo script can have its own settings file next to it:

| File | Short forms | Settings of |
|------|-------------|-------------|
| `<version>.migration.settings.json` | `<version>.m.s.json`, `<version>.m.settings.json`, `<version>.migration.s.json` | the migration script of that version, whatever its file name |
| `<version>.breaking.settings.json` | `<version>.b.s.json`, ... | the breaking script of that version |
| `<version>.undo.settings.json` | `<version>.u.s.json`, ... | the undo script of that version |

A settings file applies to its own script only. It follows the same format rules as `settings.json` and accepts three keys; a missing key falls back to the project `settings.json` (or its default), never to another item's file:

| Key | Values | Effect |
|-----|--------|--------|
| `TransactionWrapLevel` | `Migration`, `None` | `Migration` runs the script in its own transaction; `None` runs it without one. `Deployment` is a project-level value and is rejected here. |
| `TransactionIsolationLevel` | `ReadCommitted`, `RepeatableRead`, `Serializable`, `Snapshot` | Isolation level of the script's transaction, or of its connection when it runs without a transaction. |
| `Timeout` | time span | Timeout of the script's command and of its transaction. Default: the project `MigrationTimeout`. |

Typical use: the migration builds an index online without holding a long transaction on a busy table, while its breaking script, which drops an old column, keeps the default transaction.

```json
// 20260815120000.migration.settings.json
{
  "TransactionWrapLevel": "None", // online index creation
  "Timeout": "01:00:00"
}
```

Some statements cannot run inside a transaction at all: `ALTER DATABASE` and full-text DDL on SQL Server, `CREATE INDEX CONCURRENTLY`, `DROP INDEX CONCURRENTLY` and `VACUUM` on PostgreSQL. Give the script that runs them a settings file with `"TransactionWrapLevel": "None"`; the undo script that reverts them usually needs its own `<version>.undo.settings.json` too. On PostgreSQL keep such a statement alone in its file, because a file with several statements runs as one implicit transaction.

`"TransactionWrapLevel": "None"` is the author's promise that the database keeps working if the script stops halfway. A failed script is not marked as applied, so the next deployment runs the **whole** script again: write it to be re-runnable (`if not exists`, `drop ... if exists`, `create index concurrently if not exists`).

When the project uses `"TransactionWrapLevel": "Deployment"`, every script runs inside the deployment transaction. An item file with `None` then has no effect, and DbLive logs a warning.

The undo settings are stored in the database together with the undo script, so a downgrade from an older build, which no longer has these files, still uses them. Like the undo script, they are updated in the database while the undo has not run.

Errors:

| Situation | Exception |
|-----------|-----------|
| Old name `<version>.settings.json` or `<version>.s.json` | `ObsoleteMigrationSettingsFileException` (the message names the new file) |
| Settings file without its script, for example `004.breaking.settings.json` without a breaking script | `MigrationSettingsWithoutScriptException` |
| Unknown key (also the old `MigrationTimeout`), or `"TransactionWrapLevel": "Deployment"` | `InvalidMigrationItemSettingsException` |
| Any other `.json` file in `Migrations` | `UnknownMigrationSettingsException` |
