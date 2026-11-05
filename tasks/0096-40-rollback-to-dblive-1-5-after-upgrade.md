---
type: bug
priority: high
created: 2026-11-05
---
# After a deployment with DbLive 2.0, a build with DbLive 1.5.0 cannot deploy the database

## Problem
The self-migrations of 2.0 change values in the `dblive` tables that DbLive 1.5.0 cannot read, and nothing tells the user. A rollback pipeline that deploys the previous release, built with DbLive 1.5.0, after the first deployment with 2.0 is expected to fail on every run. The only way back would be to rebuild the previous release with 2.0.

What changes in the database (SQL Server / PostgreSQL self-migration):

- `07.item-settings.sql` / `04.item-settings.sql`: `dblive.migration.item_type` `settings` becomes `migration.settings`; 2.0 also writes `breaking.settings` and `undo.settings`. The `MigrationItemType` enum of 1.5.0 is `Migration, Undo, Breaking, Settings`.
- `08.relative-path-separator.sql`, `09.relative-path-lower-case.sql` / `05`, `06`: `relative_path` uses `/` and lower case. 1.5.0 looks a row up by its own path (`Code\dbo\GetUser.sql` on Windows).
- `10.code-item-type.sql` / `07.code-item-type.sql`: `dblive.code.item_type`. 2.0 also writes `status = 'removed'` (`MarkMissingCodeAsRemovedAsync`); the `CodeItemStatus` enum of 1.5.0 is `Error, Skipped, Applied`.

Expected behavior of 1.5.0 on such a database, from the code at tag `1.5.0` (not verified by an experiment):

1. Its self-deployment is skipped: `DbLiveSelfDeployer` applies only versions above `dblive.version`, which is already 10 on SQL Server.
2. Reading `dblive.migration` (`GetMigrationsAsync`, used by the checksum validation, the saver and the downgrade) fails when a row has `migration.settings`, `breaking.settings` or `undo.settings`: Dapper cannot parse it into the old enum. Every deployment fails, not only a downgrade.
3. A code file whose row is `removed` fails `FindCodeItemAsync` the same way.
4. A project without settings files and without deleted code files may still deploy, but 1.5.0 does not find the rows by path (separator, case), runs every code file again and adds rows with its own paths. What 2.0 then does with those rows is not checked.

Neither `docs/llms-full.txt` (§5.5, §18.8) nor the release notes of 2.0 say that a rollback across the upgrade needs the previous release rebuilt with 2.0.

## Where
- `src/DbLive.MSSQL/SQL/07.item-settings.sql` to `10.code-item-type.sql`
- `src/DbLive.PostgreSQL/SQL/04.item-settings.sql` to `07.code-item-type.sql`
- `src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs`: how a version handles a database deployed by a newer DbLive

## Fix
- [ ] Verify by an experiment, on SQL Server and PostgreSQL: deploy a project with 2.0 (a migration with a settings file, a deleted code file), then deploy the previous build with the 1.5.0 packages. Write down what fails and with which message.
- [ ] Release note (breaking): after the first deployment with 2.0, a build with an older DbLive cannot deploy this database; to roll back, rebuild the previous release with DbLive 2.0 first. Say it in §18.8 and on the rollback page too.
- [ ] Should 2.0 fail with a clear message when `dblive.version` is above its own last self-migration ("the database was deployed by a newer DbLive, version N; deploy with that version or newer")? It does not help 1.5.0, but the next such change is explained by the old version itself.
- [ ] 2.0 is not released yet: is any of the changes worth making readable by 1.5.0 (for example, keep `settings` for the migration settings row)? `removed` cannot be, so the note above is needed anyway.

## Docs to update
- `docs/llms-full.txt` §5.5, §15 (self-deployment of the `dblive` schema), §18.8
- `docs/deployment/migrations.md` (downgrade)

## Done when
- [ ] The behavior of a 1.5.0 build on a database deployed by 2.0 is verified and written in Notes
- [ ] Release note and docs say how to roll back across the upgrade

## Notes
Found in the code review of 2026-11-05 (#96).
