# Project Configuration

Project settings live in `Scripts/settings.json`. The file is optional; without it, or when it is empty, every setting has its default value.

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
- Time spans use `"hh:mm:ss"` or `"d.hh:mm:ss"`, for example `"01:00:00"` or `"1.00:00:00"`. **A plain number is days, not seconds**: `"60"` is 60 days, without an error, so write 30 seconds as `"00:00:30"`. Likewise `"02:00"` is 2 hours. A number without quotes (`60`) is an error.
- A list replaces the default list; it is not merged with it. An empty list is `[]`.
- **`null` is an error**, for a key (except `ProjectId`, where it means no ID) and for an item of a list. Leave a key out to keep its default.
- `ProjectId` has at most 128 characters; a longer one is an error.

An error in the file fails the deployment before anything runs, with `InvalidProjectSettingsException`. The message names the file and gives the error, with the key or the position:

```
Invalid project settings file settings.json: The JSON property 'TransactionWraplevel' could not be mapped to any .NET member contained in type 'DbLive.Common.Settings.DbLiveSettings'.
```

`RelativePath` is the file; the inner exception is the `JsonException` of `System.Text.Json`. DbLive 1.5 threw that `JsonException` itself, without the name of the file.

## Settings

### General

| Setting | Default | Description |
|---------|---------|-------------|
| `ProjectId` | none | Binds the project to the target database on the first deployment and blocks deployment to a database bound to another project. Recommended: without it every deployment logs a warning. See [ProjectId](../features/project-id.md). |
| `EnforceTimestampVersion` | `false` | Require migration versions in `yyyyMMddHHmmss` format (14 digits, valid date, the year is from 2000 to the year of next month). |
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

Folder settings are relative to the scripts folder, and `CodeSubFoldersDeploymentOrder` entries are relative to `Code`. Both can be nested paths written with either separator: `"dbo/functions"` and `"dbo\\functions"` work the same on Windows and Linux. An entry in `CodeSubFoldersDeploymentOrder` that matches no code files is logged as a warning.

No project folder may be inside another one. A file that is in two of them, such as `Code/Tests/GetUser.sql` with `"TestsFolder": "Code/Tests"`, stops the deployment with `FilesInSeveralProjectFoldersException` before any of your scripts runs: DbLive would read it twice, here as a test and as code. `"CodeFolder": ""` makes the whole `Scripts` folder the Code folder, so it fits only a project of code files: a migration next to it would run a second time as code.

### Transactions

| Setting | Default | Description |
|---------|---------|-------------|
| `TransactionWrapLevel` | `Migration` | `Migration`: each migration and breaking script in its own transaction. `Deployment`: the whole deployment in one transaction. `None`: no transactions. |
| `TransactionIsolationLevel` | `ReadCommitted` | Isolation level of deployment transactions and commands, also of a code file in its own transaction and of a command without a transaction (a BeforeDeploy or AfterDeploy script, a script with `None`). `Snapshot` is SQL Server only and requires `ALLOW_SNAPSHOT_ISOLATION ON`. SQL Server does not allow `ALTER TABLE` and index DDL in a snapshot transaction, so `Snapshot` here fails every migration in a transaction that alters a table or an index: set it in the [item settings](#item-settings) of a data migration instead. |
| `TestsTransactionIsolationLevel` | `Serializable` | Isolation level of the transaction each SQL test runs in. |

### Parallelism

| Setting | Default | Description |
|---------|---------|-------------|
| `NumberOfThreadsForCodeDeploy` | `10` | Code files deployed in parallel. Forced to 1 with `TransactionWrapLevel: Deployment`. |
| `NumberOfThreadsForTestsRun` | `10` | SQL tests run in parallel by the deployment test (`[SqlDeployFact]`) and `RunAllTestsAsync()`. |

### Timeouts

| Setting | Default | Applies to |
|---------|---------|------------|
| `MigrationTimeout` | `12:00:00` | Each migration and breaking script of a migration: the script and its transaction. An [item settings file](#item-settings) can override it with `Timeout`. |
| `DeploymentTimeout` | `1.00:00:00` | The transaction of the whole deployment (`TransactionWrapLevel: Deployment`) |
| `CodeItemTimeout` | `00:00:30` | Each code file and each [breaking script of the Code folder](../deployment/breaking-changes.md#dropping-an-old-procedure-version) |
| `UnitTestItemTimeout` | `00:01:00` | Each SQL test and its `setup.test.sql`, each on its own. The transaction of the test gets twice this value. |
| `BeforeDeployFolderTimeout` | `06:00:00` | Each BeforeDeploy script |
| `AfterDeployFolderTimeout` | `06:00:00` | Each AfterDeploy script |

The timeout of a script is the timeout of the whole file. On SQL Server that is all its `GO` batches together: DbLive runs them one by one, and each gets the time left of the file, rounded up to whole seconds, so a batch may end up to a second late, and no batch starts after the time of the file ran out. A timeout under one second is one second there. On PostgreSQL the file is one command, and DbLive cancels it when its time runs out, also when the script sends notices (`raise notice` in a loop, `drop ... if exists` of a missing object). `00:00:00` is no limit.

A script that runs longer than its timeout fails with `ExecutionTimeoutException` on both providers: `Execution Timeout Expired. The script ran longer than its timeout of 00:00:30.`. The deployment error around it names the file. The exception is a `DbLiveSqlException`; its `Timeout` is the limit, and the error of the provider is its inner exception (on PostgreSQL the cancel of the command, `OperationCanceledException`), or none when the time of a file ran out between two `GO` batches.

A transaction that runs longer than its timeout fails with `ExecutionTimeoutException` too. The message names the transaction and its setting: `Execution Timeout Expired. The deployment ran longer than its timeout of 1.00:00:00 (DeploymentTimeout).`, `The transaction of the migration ...`, `The transaction of the test ran longer than its timeout of 00:02:00 (twice UnitTestItemTimeout).` Its inner exception is the error of the step that failed after the end of the transaction; with `DeploymentTimeout` that is the error of the file where the time ran out. Nothing of the transaction is committed. On SQL Server the end of a transaction does not stop a running statement: it runs until its end or its own timeout, and the deployment fails after it.

!!! note "Transactions longer than 10 minutes"
    DbLive uses `System.Transactions`. .NET caps every transaction at the process-wide `TransactionManager.MaximumTimeout`, which is 10 minutes by default. Before opening a transaction, DbLive raises that cap to the transaction's timeout and never lowers it, so the settings above apply as written. Other `TransactionScope`s in the same process are afterwards capped at the largest timeout DbLive used.

## Choosing a transaction level

DbLive does not wrap a deployment in one transaction by default, and that is intentional (see [Database as a Service](../database-as-service.md#transactions)):

- **`Migration`** (default): each migration and breaking script commits or rolls back on its own, together with its bookkeeping. If version 5 fails, versions 1 to 4 stay applied and the next deployment continues with version 5. The database stays consistent between scripts, which is what a 24/7 system needs.
- **`Deployment`**: all or nothing, including breaking scripts. Simple for small databases and test environments, but it holds locks for the whole deployment and runs code deployment single-threaded. On PostgreSQL the deployment transaction is one session: a `SET` of one script (`search_path`, `role`, `lock_timeout`), also `SET LOCAL`, stays for the scripts after it, so a script that changes a setting restores it at its end (`reset search_path;`). On SQL Server each script gets a reset session.
- **`None`**: no transactions, except for a script whose item settings file sets `"TransactionWrapLevel": "Migration"`. Use `None` for single scripts through an [item settings file](#item-settings) rather than for the whole project.

Each SQL test always runs in its own transaction that is rolled back.

## Item settings

Each migration and breaking script can have its own settings file next to it:

| File | Short forms | Settings of |
|------|-------------|-------------|
| `<version>.migration.settings.json` | `<version>.m.s.json`, `<version>.m.settings.json`, `<version>.migration.s.json` | the migration script of that version, whatever its file name |
| `<version>.breaking.settings.json` | `<version>.b.s.json`, ... | the breaking script of that version |

A settings file applies to its own script only. It follows the same format rules as `settings.json` and accepts three keys; a missing key falls back to the project `settings.json` (or its default), never to another item's file:

| Key | Values | Effect |
|-----|--------|--------|
| `TransactionWrapLevel` | `Migration`, `None` | `Migration` runs the script in its own transaction; `None` runs it without one. `Deployment` is a project-level value and is rejected here. |
| `TransactionIsolationLevel` | `ReadCommitted`, `RepeatableRead`, `Serializable`, `Snapshot` | Isolation level of the script's transaction. Without a transaction, DbLive sets it on the connection, and the script's statements run at this level. `Snapshot` is SQL Server only: on PostgreSQL the deployment fails before anything runs. It fits a script that reads and changes data: SQL Server does not allow `ALTER TABLE` and index DDL in a snapshot transaction (Msg 3964). |
| `Timeout` | [time span](#file-format) | Timeout of the script (all its `GO` batches together, see [Timeouts](#timeouts)) and of its transaction. Default: the project `MigrationTimeout`. |

Typical use: the migration builds an index online without holding a long transaction on a busy table, while its breaking script, which drops an old column, keeps the default transaction.

```json
// 20260815120000.migration.settings.json
{
  "TransactionWrapLevel": "None", // online index creation
  "Timeout": "01:00:00"
}
```

Some statements cannot run inside a transaction at all: `ALTER DATABASE` and full-text DDL on SQL Server, `CREATE INDEX CONCURRENTLY`, `DROP INDEX CONCURRENTLY` and `VACUUM` on PostgreSQL. Give the script that runs them a settings file with `"TransactionWrapLevel": "None"`; a code file cannot run them, because it runs in a transaction of its own. A breaking script that drops such an index needs its own `<version>.breaking.settings.json` too. On PostgreSQL keep such a statement alone in its file, because a file with several statements runs as one implicit transaction: two `create index concurrently` in one file fail with `CREATE INDEX CONCURRENTLY cannot run inside a transaction block`.

The implicit transaction also means that on PostgreSQL several `update` statements in one file commit together, even with `"TransactionWrapLevel": "None"`. To commit a large update in batches, put the loop in a `do` block with a `commit` after each batch (or `call` a procedure that does it), and keep the `do` block alone in its file. Comments around it are fine; another statement in the file, or a transaction around the script, fails with `invalid transaction termination`.

```sql
-- 20260815130000.migration.sql, with "TransactionWrapLevel": "None" in its settings file
do $$
declare
    n int;
begin
    loop
        update users set active = true
        where user_id in (select user_id from users where active is null limit 10000);
        get diagnostics n = row_count;
        exit when n = 0;
        commit;
    end loop;
end $$;
```

`"TransactionWrapLevel": "None"` is the author's promise that the database keeps working if the script stops halfway. A failed script is not marked as applied, so the next deployment runs the **whole** script again: write it to be re-runnable (`if not exists`, `drop ... if exists`, `create index concurrently if not exists`).

On PostgreSQL a failed or cancelled `create index concurrently` leaves an invalid index behind (PostgreSQL behavior), and `if not exists` skips it on the next run: the deployment succeeds, and the index stays invalid, unused by queries but updated on every write. Add a `BeforeDeploy` script that drops the index if it is invalid. BeforeDeploy runs on every deployment before the migrations, so after a failure the next deployment drops the index, and the migration builds it again.

```sql
-- 20260815120000.migration.sql, with "TransactionWrapLevel": "None" in its settings file
create index concurrently if not exists ix_users_name on users (name);
```

```sql
-- BeforeDeploy/drop-invalid-ix_users_name.sql
do $$
begin
    if exists (select from pg_index where indexrelid = to_regclass('ix_users_name') and not indisvalid) then
        drop index ix_users_name;
    end if;
end $$;
```

- The `drop` cannot go into the file of the migration: with a second statement the file runs as one implicit transaction, and `create index concurrently` fails.
- `drop index concurrently` cannot run in a `do` block, so this `drop` takes a short exclusive lock on the table.
- Drop the index by name, not every invalid index: an index that another session is building concurrently right now is invalid too.
- Remove the script once every database has the index.

When the project uses `"TransactionWrapLevel": "Deployment"`, every script runs inside the deployment transaction, with its isolation level and `DeploymentTimeout`. An item file with `None` then has no effect, and DbLive logs a warning; its `TransactionIsolationLevel` is not used either, and its `Timeout` limits only the command.

Errors:

| Situation | Exception |
|-----------|-----------|
| Old name `<version>.settings.json` or `<version>.s.json` | `ObsoleteMigrationSettingsFileException` (the message names the new file) |
| Settings file without its script, for example `004.breaking.settings.json` without a breaking script | `MigrationSettingsWithoutScriptException` |
| Unknown key (also the old `MigrationTimeout`), or `"TransactionWrapLevel": "Deployment"` | `InvalidMigrationItemSettingsException` |
| Any other `.json` file in `Migrations` | `UnknownMigrationSettingsException` |
