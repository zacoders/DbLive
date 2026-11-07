# Migrations

Migrations change the **internal state** of the database: tables, columns, indexes, data. Each version is applied once per database and is never applied again. File naming is described in [Project Structure](../project/structure.md#migration-files).

## Rules of thumb

1. **Never edit a migration after it has been applied** to a shared database. Add a new version instead.
2. **Never delete an applied migration or change its version.** A build without an applied version [fails](#which-migrations-run) with `DatabaseAheadOfProjectException`.
3. **Only expand.** A migration must keep the running application working: add tables, nullable columns or columns with defaults, indexes, sync triggers. Put drops and renames of things the application still uses into the version's [breaking script](breaking-changes.md). Then the previous application runs on the new schema too, and a release is [rolled back](#roll-back-a-release) without touching the database.
4. **Fix a wrong migration with a new one.** DbLive has no undo scripts.

## Which migrations run

At the start of every deployment DbLive records all migration files of the project in `dblive.migration`. A version is **pending** when its migration item is not marked `applied`. All pending versions run in ascending version order, including versions lower than the highest applied one: a branch merged later with an older timestamp is still applied. Matching is by version and type, never by path, so you can reorganise the `Migrations` folder freely. DbLive stores the checksum of each file, not its content: the project is the source of the scripts.

Every version applied to the database must be in the project. DbLive checks it at the start of every deployment (also a breaking-only one), before it records the migration files; if the database has applied versions the project lacks, the deployment fails with `DatabaseAheadOfProjectException`, which lists those versions, before any of your scripts runs. Either an older build is deployed, and DbLive does not roll back a database (see [Roll back a release](#roll-back-a-release)), or migration files were deleted from the project: restore them.

For each pending version DbLive:

1. reads `<version>.migration.settings.json` ([item settings](../project/configuration.md#item-settings)), if any;
2. opens a transaction with the script's isolation level and timeout, unless its `TransactionWrapLevel` is `None` (or the project's `Deployment` transaction is already open);
3. runs the migration script and marks it `applied`;
4. commits.

If a script fails, the deployment stops with `MigrationDeploymentException` (the message contains the SQL error) and that version is not marked as applied. With the default `Migration` level, earlier versions stay committed, and the next deployment continues where this one stopped. A script that ran without a transaction may have been applied partly; the next deployment runs it again from the start, so it must be re-runnable.

On SQL Server, scripts may contain `GO` batch separators. On PostgreSQL, a file is sent as one multi-statement command.

## Checksums

Before deploying, DbLive compares every **applied** migration and breaking script with the file in the project. The checksum covers the text, including whitespace and comments, but not line endings: a Windows checkout (CRLF) and a Linux checkout (LF) of the same file have the same checksum.

| Situation | Result |
|-----------|--------|
| Files unchanged | Deployment continues. |
| Applied file changed, `MigrationChecksumMode: Strict` (default) | `MigrationChecksumMismatchException` listing the files. Nothing is deployed. |
| Applied file changed, `MigrationChecksumMode: Warn` | A warning per file; deployment continues. |
| Applied file changed, `RepairMigrationChecksums = true` | The stored checksums are replaced by the current ones; deployment continues. |

Files that are not applied yet can be edited freely; DbLive stores their new checksum. That includes the breaking script of a version whose breaking phase has not run yet.

Use `RepairMigrationChecksums` only when the database already matches the files, for example after a comment-only change:

```csharp
await deployer.DeployAsync(DeployParameters.Default with { RepairMigrationChecksums = true });
```

## Roll back a release

DbLive has no undo scripts and does not roll back a database. A release is rolled back like this:

- **Before the breaking phase, roll back the application and keep the database.** Migrations only add, so the previous application runs on the new schema, and the old versions of procedures (`GetUser_v1`) are still there: DbLive never drops code, and only the [breaking phase](breaking-changes.md) drops old versions. The database serves both versions of the application. Do not deploy the previous build of the database: it lacks the new versions, so it fails with `DatabaseAheadOfProjectException` and changes nothing.
- **A bug in a code object**: revert the file in git and deploy a new release. Code is [deployed by state](code.md): the reverted file differs from what was deployed, so it runs, and the old version of the object is back. A file the release had deleted runs again when it comes back.
- **A wrong migration**: fix it with a new migration. Never edit or delete an applied one.
- **A release that upgraded DbLive**: when the release notes of the new DbLive version say that older versions can no longer deploy the database, the previous build fails with `DatabaseRequiresNewerDbLiveException` and changes nothing, also when the release has no migrations of its own (see [The DbLive version a database requires](index.md#the-dblive-version-a-database-requires)). Roll back the application and keep the database.
- **The breaking phase is the point of no return.** It drops what the previous application uses. Run it only when the release is confirmed, and never for a release whose application was rolled back: no check can see that case, because the code of the release is still deployed. After the breaking phase, fix forward with a new release, or restore a backup.

A release that adds no migration version (code only) and does not upgrade DbLive past such a version can also be rolled back by deploying the previous build: it redeploys the code of that build. A breaking deployment of the newer build then fails with `CodeNotDeployedException`.

Why no undo scripts: before the breaking phase the database needs no rollback; after it, an undo would restore the structure but not the data the breaking scripts dropped; and an undo of a migration would drop the data written since the migration ran. A file `<version>.undo.sql`, `<version>.u.sql` or `<version>.undo.settings.json` in `Migrations` fails the deployment with `UndoNotSupportedException` before anything runs: delete it.

## Version numbers

Sequential numbers (`001`, `002`) work for a single developer. In a team, two branches easily pick the same number, which ends in `DuplicateMigrationItemException`. Use UTC timestamps instead:

```
20260808104925.create-users.sql
```

and enforce the format in `settings.json`:

```json
{ "EnforceTimestampVersion": true }
```

## Long-running migrations

A migration that runs inside a transaction is limited by `MigrationTimeout`, 12 hours by default (see [Timeouts](../project/configuration.md#timeouts)), and holds its locks until it commits: a large index build or data backfill blocks the tables it touches for the whole migration, and the application waits. Give such a script its own version with `"TransactionWrapLevel": "None"` in `<version>.migration.settings.json` (and a `Timeout` that fits). Without the transaction an online index build (`with (online = on)` on SQL Server, `create index concurrently` on PostgreSQL) and an update in batches that commit one by one lock a table only briefly, and the database stays online while the migration runs. On PostgreSQL a file with several statements still runs as one implicit transaction: keep `create index concurrently` alone in its file, and commit the batches in a `do` block (see [Item settings](../project/configuration.md#item-settings)). A failed script without a transaction stays partly applied, and the next deployment runs it again from the start: make it re-runnable. A failed `create index concurrently` leaves an invalid index that `if not exists` skips: drop it in a `BeforeDeploy` script ([example](../project/configuration.md#item-settings)).

## Errors

| Exception | Cause |
|-----------|-------|
| `InvalidMigrationVersionException` | The version is not a number (also a file such as `config.json` in `Migrations`), or not `yyyyMMddHHmmss` with `EnforceTimestampVersion`. |
| `DuplicateMigrationItemException` | Two files with the same version and type (`001.migration.sql` and `1.m.sql` count). |
| `MigrationItemMustExistsException` | A breaking or settings file without a migration file of the same version. |
| `UndoNotSupportedException` | An undo script or its settings file (`005.undo.sql`, `005.u.sql`, `005.undo.settings.json`): DbLive has no undo scripts. Delete the file. |
| `UnknownMigrationSettingsException` | A `.json` file in `Migrations` that is not `<version>.<item>.settings.json`. |
| `ObsoleteMigrationSettingsFileException` | The old `<version>.settings.json`; the message names the new file. |
| `MigrationSettingsWithoutScriptException` | A settings file without its script, for example `004.breaking.settings.json` without `004.breaking.sql`. |
| `InvalidMigrationItemSettingsException` | An item settings file with an unknown key (also the old `MigrationTimeout`) or `"TransactionWrapLevel": "Deployment"`. |
| `MigrationChecksumMismatchException` | An applied file was changed. |
| `DatabaseAheadOfProjectException` | Applied versions are missing from the project: an older build is deployed, or migration files were deleted. See [Roll back a release](#roll-back-a-release). |
| `MigrationDeploymentException` | A script failed; see the SQL error in the message. |
