---
type: bug
priority: medium
created: 2026-11-26
closed: 2026-11-26
breaking: true
---
# Settings accept a number that is not a value of the enum

## Release note
**An enum value in the settings is a name, not a number.** `settings.json` took a number for `TransactionWrapLevel`, `TransactionIsolationLevel`, `TestsTransactionIsolationLevel` and `MigrationChecksumMode`, also in quotes, and did not check that the setting has that value: `"TransactionWrapLevel": 3` ran every migration without a transaction, with no error. Now a number (`2`, `"2"`, also of a value that exists) and names joined by a comma (`"Migration, None"`) fail with `InvalidProjectSettingsException`, or `InvalidMigrationItemSettingsException` in an item settings file, before anything runs. The message lists the values: `The value of 'TransactionWrapLevel' must be "Deployment", "Migration" or "None". Path: $.TransactionWrapLevel.` A name works in any case, as before.

To upgrade: replace a number with the name of its value. `TransactionWrapLevel`: 0 `Deployment`, 1 `Migration`, 2 `None`. `TransactionIsolationLevel` and `TestsTransactionIsolationLevel`: 0 `ReadCommitted`, 1 `RepeatableRead`, 2 `Serializable`, 3 `Snapshot`. `MigrationChecksumMode`: 0 `Strict`, 1 `Warn`.

## Problem
`settings.json` and the item settings files read the enum keys (`TransactionWrapLevel`, `TransactionIsolationLevel`, `TestsTransactionIsolationLevel`, `MigrationChecksumMode`) with `new JsonStringEnumConverter()` (`src/DbLive/Common/Settings/SettingsTools.cs:11`). It takes a number, also in a string, and names joined by a comma, and does not check that the enum defines the result (verified, see Notes). So:
- `settings.json` `{ "TransactionWrapLevel": 3 }` loads. `DbLiveDeployer.cs:26` and `MigrationItemDeployer.cs:23, 49` compare it with `Deployment` and `Migration` only, so every migration runs without a transaction, with no error and no warning (the log says `Transaction 3`). The same for `TransactionWrapLevel` in an item settings file, for its script.
- An item settings file `{ "TransactionIsolationLevel": 7 }` passes the check of the project load (`TransactionSettingsValidator.ValidateMigrations`) on SQL Server and fails with `NotSupportedTransactionIsolationLevelException` from `TransactionScopeManager.Create` only when its migration runs, after the migrations before it are committed. On PostgreSQL it fails at the load, but the message says that PostgreSQL does not support the level `7`.
- `TestsTransactionIsolationLevel` `7` on SQL Server fails only when the tests run.
- `MigrationChecksumMode` `5` works as `Strict`, without an error.
- A defined number is a trap too: the enums are internal, their numbers are documented nowhere, and `2` meant as the second value (`Migration`) is `None`.

§9.1 (line 654): "Enum values are case-insensitive strings (`"none"`, `"None"`); numbers also work but are discouraged." The `InvalidProjectSettingsException` row of §17 (line 1103) promised the error for "an enum value that does not exist".

## Where
- `src/DbLive/Common/Settings/SettingsTools.cs:11`
- `src/DbLive/Common/Settings/DbLiveSettings.cs`, `src/DbLive/Common/Settings/MigrationItemSettings.cs` (the enum keys, also `MigrationChecksumMode`)

## Fix
- [x] Reject numbers (`new JsonStringEnumConverter(allowIntegerValues: false)`), or keep numbers and reject only a number that is not defined (`Enum.IsDefined` after the read, for each enum key)? Maintainer, 2026-11-26: neither; a converter of DbLive that takes only the name of a value, in any case, like `TimeSpanSettingConverter`. `allowIntegerValues: false` alone does not close the hole (verified, see Notes), `Enum.IsDefined` leaves the defined numbers and `"Deployment, Migration"`. Breaking for a project that writes a number: `breaking: true`, this release is a major one.

The error is `InvalidProjectSettingsException` (`settings.json`) or `InvalidMigrationItemSettingsException` (an item settings file) at the project load, and names the key and its values.

## Docs to update
- [x] `docs/llms-full.txt` §9.1 (line 653, 654), the §5 row and the §17 rows of `InvalidProjectSettingsException` and `InvalidMigrationItemSettingsException`
- [x] `docs/project/configuration.md` ("File format", "Item settings")
- [x] The doc comment of `InvalidMigrationItemSettingsException`

## Done when
- [x] Fixed, with unit tests for `settings.json` and an item settings file (a number that is not defined; a number as a string)
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
- `JsonStringEnumConverter` on net8.0, net9.0 and net10.0 (verified, a file-based app with an enum `Deployment, Migration, None`):

  | JSON | `allowIntegerValues: true` (was) | `allowIntegerValues: false` |
  |---|---|---|
  | `3` | 3, not defined | error |
  | `"3"`, `"-1"` | 3, -1 | net8.0: 3, -1; net9.0 and net10.0: error |
  | `"Migration, None"` | 3, not defined | 3 on every runtime |
  | `"Deployment, Migration"` | `Migration` | `Migration` |
  | `" Migration"` | `Migration` | `Migration` |

  Names joined by a comma are combined also for an enum without `[Flags]`. So `allowIntegerValues: false` leaves a number in a string on .NET 8 and a comma list on every runtime.
- 1.5.0 read the settings with the same `JsonStringEnumConverter`, and its enums have the same order as now (verified with `git show 1.5.0:...`): the numbers in the release note hold for a project of 1.5.
- `null` for an enum key of `settings.json` gave the error of System.Text.Json; the converter now gives the one of DbLive, as for the other keys: `The value of 'TransactionWrapLevel' cannot be null. Path: $.TransactionWrapLevel.` A nullable key of the item settings is still checked by `SettingsTools.CheckNoNulls`: System.Text.Json does not pass `null` to the converter of a `Nullable<T>` and gives `null`, while it passes `null` to the converter of a non-nullable enum (verified with a converter factory on net8.0).
- The release notes of the tasks closed since 1.5.0 do not mention the enums or a number in the settings: none to correct (searched the preview for "enum", "number", "settings.json", "null").

## Result
- `EnumSettingConverter`, a `JsonConverterFactory` for every enum key of `settings.json` and of the item settings files: a JSON string equal to the name of a value, ignoring case; everything else is an `InvalidSettingValueException` (a `JsonException`), `The value of '<key>' must be "A", "B" or "C". Path: $.<key>.`, and `cannot be null` for `null`. It replaces `JsonStringEnumConverter` in `SettingsTools.JsonSerializerOptions`. A new enum key needs no line: the factory takes every enum. `InvalidSettingValueException` is the former private `InvalidTimeSpanException` of `TimeSpanSettingConverter`, now shared by both converters.
- Decision of the maintainer of 2026-11-26: only names, no numbers. `breaking: true`.
- Tests: `EnumSettingConverterTests` (names in any case for all six enum keys of both files; `3`, `-1`, `"3"`, `"-1"`, `0`, `"1"`, `"Migration, None"`, `"Deployment, Migration"`, `" Migration"`, `"Migrations"`, `""`, `true`, `{}`, `[]`; the values listed per key; `null`; an item settings file with `7`, `"7"`, `3`, `"3"`), `SettingsTests.LoadSettings_InvalidFile_ErrorNamesFile` with `3` and `"7"` and the new message for `null`, `MigrationsTests.GetMigrations_throws_when_settings_file_is_invalid` with `7` and `"3"` (the error at the project load). With `JsonStringEnumConverter` back, 27 of them fail (verified). All 743 tests of `DbLive.Tests` pass; `DbLive` builds without warnings on net8.0, net9.0 and net10.0.
- Docs: `docs/llms-full.txt` §5, §9.1, §17, `docs/project/configuration.md`.
