---
type: bug
priority: low
created: 2026-11-09
---
# The isolation level of a migration settings file is checked only when its script runs

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
- `docs/llms-full.txt` §10 (transactions), if it says when the level is checked

## Done when
- [ ] Fixed, with a unit test: an item settings file with a level the provider does not support fails the deployment before the first migration
- [ ] Docs updated, if needed
