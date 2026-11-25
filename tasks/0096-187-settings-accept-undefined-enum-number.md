---
type: bug
priority: medium
created: 2026-11-26
---
# Settings accept a number that is not a value of the enum

## Problem
`settings.json` and the item settings files read the enum keys (`TransactionWrapLevel`, `TransactionIsolationLevel`, `TestsTransactionIsolationLevel`) with `new JsonStringEnumConverter()` (`src/DbLive/Common/Settings/SettingsTools.cs:11`). By default it takes numbers (`allowIntegerValues: true`), also in a string (`"3"`), and does not check that the number is a value of the enum. So:
- `settings.json` `{ "TransactionWrapLevel": 3 }` loads. `DbLiveDeployer.cs:26` and `MigrationItemDeployer.cs:23, 49` compare it with `Deployment` and `Migration` only, so every migration runs without a transaction, with no error and no warning (the log says `Transaction 3`).
- An item settings file `{ "TransactionIsolationLevel": 7 }` passes the check of the project load (`TransactionSettingsValidator.ValidateMigrations`) and fails with `NotSupportedTransactionIsolationLevelException` from `TransactionScopeManager.Create` only when its migration runs, after the migrations before it are committed.

§9.1 (line 654): "Enum values are case-insensitive strings (`"none"`, `"None"`); numbers also work but are discouraged." The `InvalidProjectSettingsException` row of §17 (line 1103) promises the error for "an enum value that does not exist". Read in the code, not run: verify first.

## Where
- `src/DbLive/Common/Settings/SettingsTools.cs:11`
- `src/DbLive/Common/Settings/DbLiveSettings.cs`, `src/DbLive/Common/Settings/MigrationItemSettings.cs` (the enum keys)

## Fix
- [ ] Reject numbers (`new JsonStringEnumConverter(allowIntegerValues: false)`), or keep numbers and reject only a number that is not defined (`Enum.IsDefined` after the read, for each enum key)? Rejecting numbers is breaking for a project that writes a number (`breaking: true`, and §9.1 drops "numbers also work"); this release is a major one, so it can go in now. Keeping numbers is not breaking.

Either way the error is `InvalidProjectSettingsException` (`settings.json`) or `InvalidMigrationItemSettingsException` (an item settings file) at the project load, and names the key.

## Docs to update
- `docs/llms-full.txt` §9.1 (line 654), the §17 rows of `InvalidProjectSettingsException` and `InvalidMigrationItemSettingsException`
- `docs/project/configuration.md`

## Done when
- [ ] Fixed, with unit tests for `settings.json` and an item settings file (a number that is not defined; a number as a string)
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
