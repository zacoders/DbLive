---
type: bug
priority: medium
created: 2026-11-05
rejected: 2026-11-06
---
# A downgrade uses the undo settings of a file deleted from the project

## Rejected
Rejected by the maintainer on 2026-11-06: undo scripts and the downgrade are removed (`0096-46-remove-undo.md`), so no stored undo settings can go stale; the self-migration of 0096-46 deletes the `undo` and `undo.settings` rows. The second question below (stale `breaking` and `breaking.settings` rows of a deleted file) does not depend on undo: it moved to 0096-46.

## Problem
`MigrationsSaver` adds and updates the rows of `dblive.migration` for the files of the project, but never removes the row of a file deleted from the project. `DowngradeDeployer` takes the undo settings of a version from its stored `undo.settings` row. So after `<version>.undo.settings.json` is deleted, a downgrade still applies the old settings.

That contradicts `docs/llms-full.txt` §5.5: "Without a stored undo settings file, the project settings apply", and `docs/project/configuration.md`: the undo settings "are updated in the database while the undo has not run".

Scenario (from the code, not verified by a test):

1. Release 5 has `005.undo.settings.json` with `{"TransactionWrapLevel": "None"}` and is deployed.
2. Release 6 deletes the file, so that the undo runs in a transaction, and is deployed. The `undo.settings` row of version 5 stays.
3. A rollback to release 4 with `AllowDatabaseDowngrade = true` runs `005.undo.sql` without a transaction; if it fails, it stays half applied.

The same happens with a stale `Timeout` or `TransactionIsolationLevel`, and with a deleted `<version>.undo.sql`: the downgrade still runs the stored undo script instead of stopping with `DowngradeImpossibleException`.

The downgrade is run by the older build, which does not have the version at all, so only the deployment of a build that has the version can tell that its file was deleted.

## Where
- `src/DbLive/Deployers/Migrations/MigrationsSaver.cs`
- `src/DbLive/Deployers/Migrations/DowngradeDeployer.cs:79` (reads the stored `undo.settings` rows)

## Fix
- [ ] At the deployment of a build that has the version, the saver deletes the `undo` and `undo.settings` rows of that version whose file is no longer in the project (the undo has not run: the version is applied).
- [ ] The other item types (`migration.settings`, `breaking.settings`, `breaking`): does a stale row of them matter, for example for the checksum validation or the breaking phase? Delete them by the same rule?
- [ ] Test: delete `005.undo.settings.json`, deploy, downgrade: the undo runs with the project settings. Delete `005.undo.sql`, deploy, downgrade: `DowngradeImpossibleException`.

## Docs to update
- `docs/llms-full.txt` §5.5, §15 (`dblive.migration`)
- `docs/deployment/migrations.md` (downgrade), `docs/project/configuration.md` (undo settings)

## Done when
- [ ] Fixed, with tests on SQL Server and PostgreSQL
- [ ] Docs updated

## Notes
Found in the code review of 2026-11-05 (#96).
