---
type: bug
priority: medium
created: 2026-11-17
---
# An error in `settings.json` is a bare `JsonException` that does not name the file

## Problem
`SettingsAccessor.LoadSettingsAsync` deserializes `settings.json` itself instead of calling `SettingsTools.GetSettings<T>`, so the project settings and the item settings files behave differently:

| | Item settings file | `settings.json` |
| --- | --- | --- |
| Empty file | defaults | `JsonException` |
| Unknown key, wrong enum value, invalid JSON | `InvalidMigrationItemSettingsException` with the path of the file | `JsonException` without the name of the file |

A user who upgrades to 2.0 meets this first: a `settings.json` with `MaxCodeDeployRetries` or `DowngradeTimeout`, both removed in 2.0, fails with `JsonException: The JSON property 'MaxCodeDeployRetries' could not be mapped ...`, and nothing in the message says which file to open. From the code, not run.

Found by the code review of 2026-11-17 (finding 4).

## Where
- `src/DbLive/Project/ProjectSettingsReader.cs:39-59` (`LoadSettingsAsync`)
- `src/DbLive/Common/Settings/SettingsTools.cs:14-23` (`GetSettings<T>`: an empty text gives defaults), `:26-36` (the item settings wrap `JsonException` with the path)

## Fix
Call `SettingsTools.GetSettings<DbLiveSettings>` and wrap its `JsonException` in an error that names `settings.json`.

- [ ] Which exception? A new public one (for example `InvalidSettingsException`, with the `JsonException` inside), as the item settings have, or a `JsonException` with the file name added to its message? The docs and the release note of 0096-77 name `JsonException` today.
- [ ] Should the message say that a key was removed in 2.0 when the unknown key is `MaxCodeDeployRetries` or `DowngradeTimeout`? See 0096-92.

## Docs to update
- `docs/llms-full.txt` §9.1 (line 626), §17 (the two `JsonException` rows, lines 1048 and 1079)
- `docs/project/configuration.md` ("File format")
- Release notes of the same release that name `JsonException` for `settings.json` (0096-77), when the exception changes

## Done when
- [ ] An empty `settings.json` gives the defaults; unit test in `SettingsTests`
- [ ] An unknown key, a wrong enum value and invalid JSON fail with an error that names `settings.json`; unit tests
- [ ] Docs updated
