---
type: feature
priority: high
created: 2026-10-31
closed: 2026-10-31
---
# Transaction settings per migration item: migration, breaking, undo

## Problem
Breaking and undo scripts don't follow the transaction rules of migrations, and one settings file per version can't describe items that need different settings.

- Breaking: all pending breaking scripts of a deployment run in **one** transaction (`TransactionWrapLevel: Migration`), with project settings only. `<version>.settings.json` is ignored.
- Downgrade: all undo scripts run in **one** transaction, always, regardless of `TransactionWrapLevel`, with `DowngradeTimeout`. Added in `4d29d7e` (#67) as "all or nothing. online downgrades is not required". In practice this locks the database for the whole downgrade, and an undo can't use `create index concurrently` / `online = on` without a transaction.
- `<version>.settings.json` applies to the migration script only, but breaking and undo often need other settings than the migration. Example: the migration builds an index online (`None`), its breaking script drops a column (needs a transaction).
- (was 0096-05) `MigrationItemDeployer` passes the **project** `TransactionIsolationLevel` and `MigrationTimeout` to the SQL command. The merged per-version settings reach only the `TransactionScope`, so with `TransactionWrapLevel: None` (demo `004.settings.json`) the version timeout has no effect at all.
- `"TransactionWrapLevel": "Deployment"` in a version file: `MigrationVersionDeployer` checks `== Migration`, so that version silently runs **without** a transaction.
- `UndoTestMode` runs migration, breaking and undo of a version inside the migration's transaction, with the migration's settings.

## Rule
Each item (migration, breaking, undo) is one unit: its own transaction, by its own settings, no exceptions for breaking or undo. After any item the database is consistent at the version level, so a deployment or a downgrade can stop between items (e.g. undo 004, stop, later undo 003). All-or-nothing stays available through the project-level `TransactionWrapLevel: Deployment`.

## Decisions

### Settings files
- One settings file per item: `<v>.migration.settings.json`, `<v>.breaking.settings.json`, `<v>.undo.settings.json`. Short forms combine: `m` / `b` / `u` and `s` (`<v>.m.s.json`, `<v>.b.settings.json`, ...).
- Old `<v>.settings.json` / `<v>.s.json`: error with a hint to the new name.
- A settings file without its script (e.g. `<v>.breaking.settings.json` without `<v>.breaking.sql`): error.
- Keys: `TransactionWrapLevel`, `TransactionIsolationLevel`, `Timeout` (was `MigrationTimeout`). The old key already fails: unknown keys are disallowed (`SettingsTools.JsonSerializerOptions`).
- `TransactionWrapLevel` in an item file: `Migration` or `None`. `Deployment` is an error when the file is read.

### Defaults
- A missing key falls back to the **project** settings, never to another item's file.
- `Timeout` default for all three item types: project `MigrationTimeout`.
- Remove `DbLiveSettings.DowngradeTimeout`.

### Execution
- Migration, breaking, undo: with `Migration` level, a transaction per item with the item's isolation level and timeout; the same isolation level and timeout go to the SQL command (was 0096-05).
- Breaking: per version, in version order, settings from `<v>.breaking.settings.json`.
- Downgrade: per version, newest first, settings from the stored `undo.settings` row merged with the **current** project settings.
- `UndoTestMode`: each step (migration, breaking, undo, migration again) runs with its own settings in its own transaction.
- Project `Deployment` + item `None`: log a warning and run the item inside the deployment transaction.
- `None` is the author's guarantee that the database keeps working if the script stops midway (e.g. index creation). The whole script reruns on the next deploy, so it must be rerunnable (`if not exists`). Say this in the docs.

### Storage
- Enum `MigrationItemType`: `Settings` becomes `MigrationSettings`, new `BreakingSettings` and `UndoSettings`.
- Values in `dblive.migration.item_type` follow the file names, like `migration`, `undo`, `breaking` do: `migration.settings`, `breaking.settings`, `undo.settings`. `item_type` is `varchar(32)`, no schema change.
- One mapper between the enum and the DB value, replacing `ItemType.ToString().ToLower()` in both DAs and Dapper's enum parsing on read.
- Self-deploy scripts (MSSQL `07.*.sql`, PostgreSQL `04.*.sql`): `update dblive.migration set item_type = 'migration.settings' where item_type = 'settings'`.
- Settings rows are saved as now (hash, no content). `undo.settings` also saves its content, like `undo`, and is updated while the undo is not applied. Checksum validation unchanged.

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs`, `MigrationItemType.cs`, `DbLiveProject.cs:80-140` (item checks)
- `src/DbLive/Common/Settings/MigrationSettings.cs`, `SettingsTools.cs:25` (`GetMigrationSettings`), `DbLiveSettings.cs` (`DowngradeTimeout`)
- `src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:24-30` (command uses project settings)
- `src/DbLive/Deployers/Migrations/MigrationVersionDeployer.cs` (settings, `UndoTestMode`)
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs:63-76`
- `src/DbLive/Deployers/Migrations/DowngradeDeployer.cs:84-107`
- `src/DbLive/Deployers/Migrations/MigrationsSaver.cs:53` (content for `undo.settings`)
- `src/DbLive.MSSQL/MsSqlDA.cs`, PostgreSQL DA (item type mapping, read undo settings)
- `src/DbLive.MSSQL/SQL/`, `src/DbLive.PostgreSQL/SQL/` (rename `settings` rows)
- Demo: `src/Demo/DemoMSSQL/Scripts/Migrations/004.settings.json`, `src/Demo/Demo.PostgreSQL.Chinook/Scripts/Migrations/20260208142812.settings.json` (rename, `MigrationTimeout` → `Timeout`)

## Docs to update
- `docs/llms-full.txt` §4 (discovery), §5.1, §5.3, §5.5, §5.6, §9.2, §9.3, §10, §17, §18.7, §18.8
- `docs/llms.txt` (file name grammar)
- `docs/project/configuration.md` (timeouts, transactions, "Per-migration settings")
- `docs/project/structure.md` (file names)
- `docs/deployment/migrations.md` (downgrade, exceptions)
- `docs/deployment/breaking-changes.md` ("When breaking scripts run")
- `docs/features/index.md`, `docs/philosophy.md`, `docs/examples/index.md` (`004.settings.json` mentions)

## Done when
- [x] Parser: new settings file names, errors for old names and for a settings file without its script
- [x] Item settings model with `Timeout`; `Deployment` in an item file is an error
- [x] Item settings reach the transaction and the SQL command
- [x] Breaking: transaction per version by its own settings
- [x] Downgrade: transaction per version by stored undo settings; `DowngradeTimeout` removed
- [x] `UndoTestMode`: each step by its own settings
- [x] Warning for project `Deployment` + item `None`
- [x] Item type mapper; `undo.settings` content saved; self-deploy rename scripts (MSSQL, PostgreSQL)
- [x] Demos renamed
- [x] Unit tests, integration tests (MSSQL, PostgreSQL) green
- [x] Docs updated

## Notes
- File name grammar: for `.json` the second part is the script type and the third `settings`/`s`; an optional fourth part is the name. For `.sql` nothing changed, except that `settings`/`s` is no longer a type token: `005.settings.sql` is a migration named `settings` (before: an error).
- `001.migration.json` (a type without `settings`) is now `UnknownMigrationSettingsException`, not `InvalidMigrationItemTypeException`.
- Item settings are parsed when the project is read (`DbLiveProject.GetMigrationsAsync`), so a bad file or `Deployment` stops the deployment before anything runs. JSON errors are wrapped in `InvalidMigrationItemSettingsException` with the file path.
- `MigrationItemDeployer` owns the item transaction now (merge with the project settings, warning, `TransactionScope`, command isolation and timeout). `MigrationVersionDeployer`, `BreakingChangesDeployer` and `DowngradeDeployer` only pick the settings and call it.
- Project `Deployment`: an item never opens its own scope, also with item `Migration` (a nested `Required` scope with another isolation level would throw).
- Item isolation level is checked for the provider when the item runs (`Snapshot` on PostgreSQL → `NotSupportedTransactionIsolationLevelException`), like the project level at the start.
- `MigrationItemSettings` (was `MigrationSettings`) is a record, so tests compare settings by value.
- `MigrationItemRow` reads `dblive.migration` in both DAs; `item_type` goes through `MigrationItemTypeMapper`. `DbLive.PostgreSQL.Tests` got `InternalsVisibleTo` (for `UndoTestDeployment`), like `DbLive.MSSQL.Tests`.
- The demo `004.migration.sql` runs without a transaction but is not re-runnable (`create index` without a check). Not changed here: editing an applied migration breaks checksums of existing databases. See `0096-14`.

## Result
Each migration, breaking and undo script is one unit: its own transaction, by its own settings file merged over the project settings.

- Settings files: `<v>.migration.settings.json`, `<v>.breaking.settings.json`, `<v>.undo.settings.json` (short forms `m`/`b`/`u` + `s`/`settings`). Keys `TransactionWrapLevel` (`Migration`/`None`), `TransactionIsolationLevel`, `Timeout`. New exceptions: `ObsoleteMigrationSettingsFileException` (old `<v>.settings.json`), `MigrationSettingsWithoutScriptException`, `InvalidMigrationItemSettingsException` (unknown key incl. `MigrationTimeout`, bad JSON, `Deployment`).
- Execution: the item's isolation level and timeout go to the `TransactionScope` and to the SQL command (fixes 0096-05). Breaking: one transaction per version. Downgrade: one transaction per version, newest first, by the stored `undo.settings` over the current project settings. `UndoTestMode`: each step by its own settings. Project `Deployment` + item `None`: warning, runs inside the deployment transaction. `DbLiveSettings.DowngradeTimeout` removed.
- Storage: `MigrationItemType.MigrationSettings` / `BreakingSettings` / `UndoSettings`, stored as `migration.settings` / `breaking.settings` / `undo.settings` via `MigrationItemTypeMapper`. `undo.settings` content is saved. Self-deploy scripts MSSQL `07.item-settings.sql`, PostgreSQL `04.item-settings.sql` rename old `settings` rows.
- Demos: `004.migration.settings.json`, `20260208142812.migration.settings.json` with `Timeout`.
- Tests: unit tests for parser, mapper, project checks, item/version/breaking/downgrade deployers, saver (253 in `DbLive.Tests`). Integration tests `ItemTransactionSettingsTests` for MSSQL (`alter database`) and PostgreSQL (`create/drop index concurrently`, `vacuum`): undo test mode, breaking, downgrade by stored undo settings, a script without settings runs in a transaction, self-deploy rename of old rows. All test projects green, including the demo deployment tests.
- Docs: `docs/llms-full.txt` §0 (rule 13), §4, §5.1-5.6, §9.2, §9.3, §10, §15, §16, §17, §18.7, §18.8, §19; `docs/llms.txt`; `docs/project/configuration.md` ("Item settings", timeouts, transactions); `docs/project/structure.md`; `docs/project/index.md`; `docs/deployment/migrations.md`; `docs/deployment/breaking-changes.md`; `docs/features/index.md`; `docs/examples/index.md`; `docs/philosophy.md` (the `004.settings.json` sentence only).

Release note:

> **Breaking: per-script settings files and transactions.** `<version>.settings.json` is replaced by one file per script: `<version>.migration.settings.json`, `<version>.breaking.settings.json`, `<version>.undo.settings.json`; rename existing files and the key `MigrationTimeout` to `Timeout` (an old name fails with a hint). `Timeout` now also sets the command timeout. Breaking scripts and undo scripts no longer share one transaction: each runs in its own transaction by its own settings file, so a downgrade or a breaking deployment can stop between versions. `DowngradeTimeout` is removed from `settings.json`. Use `"TransactionWrapLevel": "Deployment"` for all-or-nothing. Existing `settings` rows in `dblive.migration` are renamed to `migration.settings` automatically.
