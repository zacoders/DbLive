---
type: bug
priority: medium
created: 2026-11-17
closed: 2026-11-22
breaking: true
---
# An error in `settings.json` is a bare `JsonException` that does not name the file

## Release note
**An error in `settings.json` names the file.** An unknown key, a wrong value or invalid JSON in `settings.json` fails with the new `InvalidProjectSettingsException`: `Invalid project settings file settings.json: <error>`, with `RelativePath` and the `JsonException` of `System.Text.Json` as the inner exception. DbLive 1.5 threw the bare `JsonException`, which did not say which file was wrong. An empty `settings.json` now means the default settings, as a missing one does; it failed before.

To upgrade: code or a test that catches `JsonException` for an invalid `settings.json` catches `InvalidProjectSettingsException` instead.

## Problem
`SettingsAccessor.LoadSettingsAsync` deserializes `settings.json` itself instead of calling `SettingsTools.GetSettings<T>`, so the project settings and the item settings files behave differently:

| | Item settings file | `settings.json` |
| --- | --- | --- |
| Empty file | defaults | `JsonException` |
| Unknown key, wrong enum value, invalid JSON | `InvalidMigrationItemSettingsException` with the path of the file | `JsonException` without the name of the file |

A user who upgrades to 2.0 meets this first: a `settings.json` with `MaxCodeDeployRetries` or `DowngradeTimeout`, both removed in 2.0, fails with `JsonException: The JSON property 'MaxCodeDeployRetries' could not be mapped ...`, and nothing in the message says which file to open. Verified with a temporary test through the real `SettingsAccessor` (not kept):

| `settings.json` | Before the fix |
| --- | --- |
| empty, or only a comment | `JsonException: The input does not contain any JSON tokens ...` |
| `{ "MaxCodeDeployRetries": 5 }` | `JsonException: The JSON property 'MaxCodeDeployRetries' could not be mapped to any .NET member contained in type 'DbLive.Common.Settings.DbLiveSettings'.` |
| `{ "TransactionWrapLevel": "Nope" }` | `JsonException: The JSON value could not be converted to DbLive.Common.TransactionWrapLevel. Path: $.TransactionWrapLevel ...` |
| a trailing comma | `JsonException: The JSON object contains a trailing comma ...` |

Only the unknown key hints at the file, through the type name `DbLiveSettings`; `TransactionWrapLevel` is a key of the item settings files too.

Found by the code review of 2026-11-17 (finding 4).

## Where
- `src/DbLive/Project/ProjectSettingsReader.cs:39-59` (`LoadSettingsAsync`)
- `src/DbLive/Common/Settings/SettingsTools.cs:14-23` (`GetSettings<T>`: an empty text gives defaults), `:26-36` (the item settings wrap `JsonException` with the path)

## Fix
Call `SettingsTools.GetSettings<DbLiveSettings>` and wrap its `JsonException` in an error that names `settings.json`.

- [x] Which exception? A new public one (for example `InvalidSettingsException`, with the `JsonException` inside), as the item settings have, or a `JsonException` with the file name added to its message? The docs and the release note of 0096-77 name `JsonException` today.
  Maintainer, 2026-11-22: a new public exception, `InvalidProjectSettingsException`, `breaking: true`.
- [x] Should the message say that a key was removed in 2.0 when the unknown key is `MaxCodeDeployRetries` or `DowngradeTimeout`? See 0096-92.
  Maintainer, 2026-11-22: no. The message names the file and the key; the row of §17 (0096-92) gives the cause and the fix.

## Docs to update
- `docs/llms-full.txt` §9.1, §17 (the row of `settings.json` and the row of `DowngradeTimeout`)
- `docs/project/configuration.md` (intro, "File format")
- Release notes of the same release that name `JsonException` for `settings.json`: 0096-77, 0096-46

## Done when
- [x] An empty `settings.json` gives the defaults; unit test in `SettingsTests`
- [x] An unknown key, a wrong enum value and invalid JSON fail with an error that names `settings.json`; unit tests
- [x] Docs updated

## Notes
- The exception follows `InvalidMigrationItemSettingsException`: sealed, internal constructor, `RelativePath`, message `Invalid project settings file <path>: <error of System.Text.Json>`. `RelativePath` is the file as the project has it (`Settings.JSON` stays `Settings.JSON`).
- Precedent for `breaking: true`: 0096-63 replaced `DbLiveSqlException` with `FolderDeploymentException` and was `breaking: true`. 2.0 already has breaking tasks, so the version does not change.
- A file with only a comment still fails, as an item settings file does: `GetSettings<T>` gives the defaults for an empty or whitespace text only. Not changed: rare, and the error now names the file.
- Settings are read when the project is loaded (`ProjectLoader`), before the database is opened, so nothing ran when the exception is thrown.

## Result
- `InvalidProjectSettingsException` (namespace `DbLive`, `PublicAPI.Shipped.txt`): `SettingsAccessor.LoadSettingsAsync` calls `SettingsTools.GetSettings<DbLiveSettings>` and wraps its `JsonException`. An empty or whitespace `settings.json` gives the defaults.
- Tests in `SettingsTests`: `LoadSettings_EmptyFile_DefaultSettings` (empty and whitespace), `LoadSettings_InvalidFile_ErrorNamesFile` (unknown key, wrong case of a key, wrong enum value, trailing comma, truncated JSON in `Settings.JSON`: type, `RelativePath`, message, inner `JsonException`). All 549 tests of `DbLive.Tests` pass; `src/DbLive.sln` builds with no warning.
- Docs: `docs/llms-full.txt` §9.1 and §17, `docs/project/configuration.md`. The release notes of 0096-46 and 0096-77 name the new exception; 0096-92 writes its row with the new text.
