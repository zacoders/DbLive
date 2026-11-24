---
type: bug
priority: medium
created: 2026-11-24
closed: 2026-11-24
breaking: true
---
# `null` is the project value in the item settings and no ID in `ProjectId`

## Release note
**`null` is an error in every settings file, also for `ProjectId`.** `"ProjectId": null` in `settings.json` meant no ID; now it fails with `InvalidProjectSettingsException`, before anything runs: `Invalid project settings file settings.json: The value of 'ProjectId' cannot be null. Path: $.ProjectId.` The same holds for every key of an item settings file (`<version>.migration.settings.json`, `<version>.breaking.settings.json`): `"Timeout": null` fails with `InvalidMigrationItemSettingsException`, `The value of 'Timeout' cannot be null. Path: $.Timeout.`, and a file that is only `null` fails as well. Leave a key out to keep its default or the project value.

To upgrade: remove `"ProjectId": null` from `settings.json`.

## Problem
0096-119 made `null` an error for the keys of `settings.json`, except `ProjectId`, where `null` meant no ID. In the item settings files `null` was the project value, the same as a missing key: `MigrationItemSettings` has only nullable keys, and System.Text.Json sets them to `null` for both. A file that is only `null` gave the defaults. The docs said the opposite for the item settings: §9.3 "Same JSON rules as `settings.json` (Section 9.1)", and §9.1 "`null` is an error for every key but `ProjectId`".

## Fix
- [x] Is `null` an error everywhere, also for `ProjectId` and in the item settings? Maintainer, 2026-11-24: yes, `null` is an error for every value: either leave a key out or give it a correct value.

## Docs to update
- `docs/llms-full.txt` §9.1 (`null`, the errors of DbLive), §9.2 (the default of `ProjectId`), §9.3, §5 and §17 (the rows of both exceptions)
- `docs/project/configuration.md`: the file format, the item settings and their errors
- `docs/features/project-id.md`: "ProjectId not set"
- The XML doc of `InvalidMigrationItemSettingsException`

## Done when
- [x] `null` for a key, for an item of a list and for the whole file fails in `settings.json` and in the item settings files, with unit tests
- [x] Docs updated

## Notes
- Found while working on 0096-147: its test `Null_timeout_of_an_item_is_the_project_value` pinned the old behavior.
- System.Text.Json cannot tell `"Timeout": null` from a missing key after the deserialization, so the check reads the JSON itself: `SettingsTools.CheckNoNulls` parses the file with `JsonDocument` and checks the keys and the items of the lists. The settings have no nested objects. It replaces the list of keys of 0096-119 in `SettingsAccessor.CheckNoNulls` (`src/DbLive/Project/ProjectSettingsReader.cs`), which needed a line for every new key of a reference type; the messages stay the same.
- The check runs after the deserialization, so invalid JSON and an unknown key keep the error of System.Text.Json: `{ "MaxCodeDeployRetries": null }` is an unknown key, not a `null`. For a key of an enum, a number or `true`/`false` in `settings.json`, `null` fails in System.Text.Json first ("The JSON value could not be converted to ..."), as it did before; a time span gets `cannot be null` from the converter of 0096-147.
- The item settings files of each script are new in this release (0096-13), so their `null` changes nothing for an upgrade from 1.5. DbLive 1.5 read `"ProjectId": null` as no ID, and a file that is only `null` as the defaults.

## Result
- `null` is an error in every settings file: for every key, also `ProjectId` and the keys of the item settings, for an item of a list and for the whole file. The message names the key: `The value of '<key>' cannot be null. Path: $.<key>.`, `An item of '<key>' cannot be null. Path: $.<key>[<index>].`, `The file cannot be null. Path: $.`. One check for both files, `SettingsTools.CheckNoNulls`, instead of the list of keys in `SettingsAccessor`. Decision of the maintainer of 2026-11-24. `breaking: true`: `"ProjectId": null` loaded in 1.5.
- Tests: `SettingsTests.LoadSettings_InvalidFile_ErrorNamesFile` with `"ProjectId": null`, `"TransactionWrapLevel": null`, a file `null` and an unknown key set to `null` (the error of an unknown key, verified); `MigrationsTests.GetMigrations_throws_when_settings_file_is_invalid` with `null` for each of the three keys and a file `null`. With the check switched off, 15 cases fail: these and the nine of 0096-119 (verified). All 672 tests of `DbLive.Tests` pass; `DbLive` builds without warnings on net8.0, net9.0 and net10.0.
- Docs: `docs/llms-full.txt` §9.1, §9.2, §9.3, §5, §17, `docs/project/configuration.md`, `docs/features/project-id.md`.
