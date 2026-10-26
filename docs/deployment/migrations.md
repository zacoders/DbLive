# Migrations

Migrations change the **internal state** of the database: tables, columns, indexes, data. Each version is applied once per database and is never applied again. File naming is described in [Project Structure](../project/structure.md#migration-files).

## Rules of thumb

1. **Never edit a migration after it has been applied** to a shared database. Add a new version instead.
2. **Never delete an applied migration or change its version.** DbLive would treat the missing version as a [downgrade](#downgrade).
3. **Only expand.** A migration must keep the running application working: add tables, nullable columns or columns with defaults, indexes, sync triggers. Put drops and renames of things the application still uses into the version's [breaking script](breaking-changes.md).
4. **Write an undo script** for every version you may need to roll back.

## Which migrations run

At the start of every deployment DbLive records all migration files of the project in `dblive.migration`. A version is **pending** when its migration item is not marked `applied`. All pending versions run in ascending version order, including versions lower than the highest applied one: a branch merged later with an older timestamp is still applied. Matching is by version and type, never by path, so you can reorganise the `Migrations` folder freely.

For each pending version DbLive:

1. reads the version's [settings file](../project/configuration.md#per-migration-settings), if any;
2. opens a transaction (unless the effective `TransactionWrapLevel` is `None`);
3. runs the migration script and marks it `applied`;
4. commits.

If a script fails, the deployment stops with `MigrationDeploymentException` (the message contains the SQL error) and that version is not marked as applied. With the default `Migration` level, earlier versions stay committed, and the next deployment continues where this one stopped.

On SQL Server, scripts may contain `GO` batch separators. On PostgreSQL, a file is sent as one multi-statement command.

## Checksums

Before deploying, DbLive compares every **applied** migration and breaking script with the file in the project. The checksum covers the exact text, including whitespace, comments and line endings.

| Situation | Result |
|-----------|--------|
| Files unchanged | Deployment continues. |
| Applied file changed, `MigrationChecksumMode: Strict` (default) | `MigrationChecksumMismatchException` listing the files. Nothing is deployed. |
| Applied file changed, `MigrationChecksumMode: Warn` | A warning per file; deployment continues. |
| Applied file changed, `RepairMigrationChecksums = true` | The stored checksums are replaced by the current ones; deployment continues. |

Files that are not applied yet can be edited freely; DbLive stores their new content and checksum. That includes undo scripts of applied versions, so an undo script can be fixed after its migration went to production.

Use `RepairMigrationChecksums` only when the database already matches the files, for example after a comment-only change or after normalising line endings:

```csharp
await deployer.DeployAsync(DeployParameters.Default with { RepairMigrationChecksums = true });
```

## Undo scripts

`<version>.undo.sql` reverts its version. DbLive stores the undo script's content in the database when it records the migration files. A downgrade can therefore run it even when the build being deployed no longer contains the file.

Write undo scripts for both possible states, because the breaking script of the version may or may not have run:

```sql
-- 007.undo.sql: restore the column if the breaking script removed it, then drop the new objects
if not exists (select * from sys.columns
               where object_id = object_id('dbo.Person') and name = 'PhoneNumber')
begin
    alter table dbo.Person add PhoneNumber varchar(32) null;

    exec('update p set PhoneNumber = pp.PhoneNumber
          from dbo.Person p
          join dbo.PersonPhones pp on pp.PersonId = p.PersonId and pp.IsPrimary = 1');
end

drop trigger if exists dbo.TMP_DataMigration_Person_To_PersonPhones;
drop table if exists dbo.PersonPhones;
```

Undo scripts are tested by the `[SqlDeployFact]` deployment test, which applies each version as migration, (breaking,) undo, migration again. See [Unit Testing](../unit-testing/index.md#deployment-tests).

## Downgrade

A downgrade happens when you deploy a build whose `Migrations` folder lacks versions that are applied in the database, usually because you are rolling back to the previous release.

- By default DbLive refuses: `DowngradeNotAllowedException`, and nothing changes.
- With `AllowDatabaseDowngrade = true`, DbLive runs the stored undo scripts of those versions, newest first, **in one transaction**. If any of them has no undo script, it stops with `DowngradeImpossibleException` before running anything.
- After the undo, the version's migration and breaking items are marked `reverted`. Deploying the newer build again re-applies them.
- Code objects are not dropped. Code files of the older build are redeployed if they differ from what was last deployed.

```csharp
// Roll back to the previous release: deploy its build with downgrade allowed.
await deployer.DeployAsync(DeployParameters.Default with
{
    AllowDatabaseDowngrade = true,
    RunTests = false
});
```

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

A migration that runs inside a transaction is limited by `MigrationTimeout`, 12 hours by default (see [Timeouts](../project/configuration.md#timeouts)), and holds its locks until it commits. Large index builds and data backfills should get their own version with `"TransactionWrapLevel": "None"` in a settings file, should be re-runnable, and should update data in batches.

## Errors

| Exception | Cause |
|-----------|-------|
| `InvalidMigrationVersionException` | The version is not a number, or not `yyyyMMddHHmmss` with `EnforceTimestampVersion`. |
| `DuplicateMigrationItemException` | Two files with the same version and type (`001.migration.sql` and `1.m.sql` count). |
| `MigrationItemMustExistsException` | An undo, breaking or settings file without a migration file of the same version. |
| `UnknownMigrationSettingsException` | A `.json` file in `Migrations` that is not `<version>.settings.json`. |
| `MigrationChecksumMismatchException` | An applied file was changed. |
| `DowngradeNotAllowedException` | Applied versions are missing from the project. |
| `DowngradeImpossibleException` | A version to revert has no undo script. |
| `MigrationDeploymentException` | A script failed; see the SQL error in the message. |
