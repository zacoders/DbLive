---
type: bug
priority: medium
created: 2026-10-26
---
# Migration timeout and isolation level don't reach the SQL command

## Problem
`MigrationTimeout` and `TransactionIsolationLevel` from `<version>.settings.json` apply only to the `TransactionScope`. The SQL command takes its values from the project settings. With `TransactionWrapLevel: None`, as in the demo `004.settings.json`, the migration timeout has no effect at all.

## Where
`src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:28-29` uses `projectSettings`; the merged settings exist only in `MigrationVersionDeployer.cs:35-36`.

## Fix
Pass the merged `MigrationSettings` to `MigrationItemDeployer.DeployAsync` and use them in `ExecuteNonQueryAsync`.

## Docs to update
- `docs/llms-full.txt` §9.3 (keys table), §19
- `docs/project/configuration.md` (table "Per-migration settings")

## Done when
- [ ] Merged settings used for the command, with a unit test
- [ ] Docs updated
