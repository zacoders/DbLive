---
type: bug
priority: low
created: 2026-11-09
closed: 2026-11-15
---
# The isolation level of a migration settings file is checked only when its script runs

## Release note
A migration or breaking settings file with a `TransactionIsolationLevel` the provider does not support (`Snapshot` on PostgreSQL) now fails the deployment at its start, before anything is deployed. Before, the migrations ahead of it were applied and committed first. The message of `NotSupportedTransactionIsolationLevelException` now names the settings file and the provider. Nothing to change on upgrade.

## Problem
`TranIsolationLevelMapper.ValidateForProvider` checks the `TransactionIsolationLevel` of a migration or breaking settings file only in `MigrationItemDeployer`, right before that script runs. The read of the project checks only the JSON and `TransactionWrapLevel: Deployment` of the file, and `TransactionSettingsValidator` checks only `settings.json`.

Example (from the code, not verified): PostgreSQL, `005.migration.settings.json` with `"TransactionIsolationLevel": "Snapshot"`. With `TransactionWrapLevel: Migration`, migrations 001 to 004 are applied and committed, then 005 fails with `NotSupportedTransactionIsolationLevelException`. A breaking settings file fails the same way, in the breaking phase. The other errors of the project stop the deployment before anything is deployed (§4).

A mistake in the project, so low priority: nothing is deployed wrong, the deployment only stops later than it could.

The project does not know the provider, so its read cannot check the level. `TransactionSettingsValidator` knows it (`_da.Provider`).

Found by the code review of 2026-11-09 (finding 8).

## Where
- `src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:21` (the only check)
- `src/DbLive/Project/DbLiveProject.cs:184` (the settings files are parsed while the project is read)
- `src/DbLive/Common/TransactionSettingsValidator.cs:12-13`

## Fix
`TransactionSettingsValidator` also checks the `TransactionIsolationLevel` of the settings files of every migration of the project (migration and breaking). With 0096-65 it runs before the self-deployment too.

## Docs to update
- `docs/llms-full.txt` §9.3, §12 step 1, §16, §17 (`NotSupportedTransactionIsolationLevelException`)
- `docs/project/configuration.md` ("Item settings")

## Done when
- [x] Fixed, with a unit test: an item settings file with a level the provider does not support fails the deployment before the first migration
- [x] Docs updated, if needed

## Notes
- The message of the exception did not name the file: `Unsupported transaction isolation level 'Snapshot'.` With several settings files the user had to look for it. Now: `Transaction isolation level 'Snapshot' in Migrations/005.migration.settings.json is not supported by PostgreSQL.`; from `settings.json`, without the file.
- The check covers every migration of the project, applied ones too: the project load does not know the database (it may not exist yet). An applied migration with such a settings file could not have run: `ValidateForProvider` and `ToSql` rejected `Snapshot` on PostgreSQL before this change too. Only a settings file added to an applied migration later was ignored and now fails the deployment; it has no effect anyway, so delete it. Not breaking.
- The check in `MigrationItemDeployer` stays: it checks the effective level of the script (item or `settings.json`), and costs nothing.
- `docs/llms-full.txt` §17 said "when that script runs" and §16 said "deployment fails at start": they disagreed for an item settings file. Both now say at the start.

## Result
- `ITransactionSettingsValidator.ValidateMigrations` checks the `TransactionIsolationLevel` of every migration and breaking settings file against the provider. `ProjectLoader` calls it right after the migrations are read, so the error stops the deployment before the database is created, before the self-deployment and before the first migration.
- `TranIsolationLevelMapper.ValidateForProvider` takes the path of the settings file for the message; the message names the provider. The two checks for PostgreSQL there are one.
- `NotSupportedTransactionIsolationLevelException` has a new internal constructor; the public API did not change.
- Tests: `TransactionSettingsValidatorTests` (`Snapshot` in a migration and in a breaking settings file on PostgreSQL fails with the path in the message; allowed on SQL Server; files without a level or with a supported one pass), `ProjectLoaderTests` (the load with the real project and validator fails on PostgreSQL; the loader passes the migrations to the validator). `DbLive.Tests` 483 pass; the solution builds without warnings.
- Docs: `docs/llms-full.txt` §9.3, §12, §16, §17; `docs/project/configuration.md`.
