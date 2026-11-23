---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
breaking: true
---
# A `null` value in `settings.json` fails with `NullReferenceException`

## Release note
**`null` in `settings.json` is an error that names the key.** `null` for a folder key, `TestFilePatterns` or `CodeSubFoldersDeploymentOrder` failed the load of the project with a `NullReferenceException` that named neither the file nor the key. A `null` item of `TestFilePatterns` matched every file of `Code/`: every code file ran as a test, and none was deployed. Both now fail with `InvalidProjectSettingsException`, before anything runs: `Invalid project settings file settings.json: The value of 'CodeFolder' cannot be null. Path: $.CodeFolder.`, `... An item of 'TestFilePatterns' cannot be null. Path: $.TestFilePatterns[1].`

To upgrade: remove a key set to `null` to keep its default, write `[]` for an empty list, and remove `null` items from lists.

## Problem
It takes a mistake in `settings.json` to get here, but the docs promise a clear error for it: `docs/llms-full.txt` §9.1 (line 637) and `docs/project/configuration.md:32` say that an error in the file ("a wrong value") fails with `InvalidProjectSettingsException`, which names the file and the key.

`"MigrationsFolder": null` (or another folder key), `"TestFilePatterns": null` and `"CodeSubFoldersDeploymentOrder": null` deserialize without an error and store `null`, because the net8.0 System.Text.Json does not enforce nullable annotations. The project load then fails with a bare `NullReferenceException` that names neither the file nor the key:
- a folder: `ToFolder(null)` (`src/DbLive/Project/DbLiveProject.cs:396`);
- `TestFilePatterns`: `DbLiveProject.cs:47`;
- `CodeSubFoldersDeploymentOrder`: the `foreach` at `DbLiveProject.cs:57`, when the project has code files.

It still fails before anything touches the server. The deserialization was verified by experiment on 2026-11-23; the exceptions by reading the code.

## Fix
After `GetSettings<DbLiveSettings>` in `SettingsAccessor.LoadSettingsAsync` (`src/DbLive/Project/ProjectSettingsReader.cs:55`), check the reference-type properties for `null` and throw `InvalidProjectSettingsException(settingsPath, "The value of 'X' cannot be null.", ...)`. `RespectNullableAnnotations` exists only from System.Text.Json 9.

- [x] Also check the items of the lists? Maintainer, 2026-11-23: yes, together with the keys.

## Docs to update
- `docs/llms-full.txt` §9.1 and the row of `InvalidProjectSettingsException` in the table of exceptions
- `docs/project/configuration.md` (file format)

## Done when
- [x] Fixed, with unit tests for a folder key, `TestFilePatterns` and `CodeSubFoldersDeploymentOrder` set to `null`, and for a `null` item of each list
- [x] Docs updated

## Notes
- A `null` item of a list is worse than a `null` list. Verified on 2026-11-23 by experiment: `[null]` deserializes into a `null` item, also with `RespectNullableAnnotations`, and `FileSystemName.TranslateWin32Expression(null)` is `"*"`, which matches every file name. Read in the code: a `null` item of `TestFilePatterns` makes every file of `Code/` a test and none a code file, without an error (unless `Code/` has a breaking script: `BreakingScriptMatchesTestPatternException`); a `null` item of `CodeSubFoldersDeploymentOrder` fails in `ToFolder(null)` when the project has code files.
- `RespectNullableAnnotations` (System.Text.Json 9+, net9.0 and net10.0 only) was rejected: on net8.0 it needs a package reference to System.Text.Json 9 or an `#if` with different behavior per target, and it does not check list items (verified on 2026-11-23). Its message would have been "The property or field 'X' on type 'Y' doesn't allow setting null values".
- A `null` for the other keys was already an error: an enum, `TimeSpan`, `int` or `bool` key fails in System.Text.Json ("The JSON value could not be converted to ...", verified on 2026-11-23 on net8.0). `ProjectId` is `string?`: `null` means no ID. `MigrationItemSettings` has only nullable value types, where `null` means the project value: nothing to check.
- Before the fix, `CodeSubFoldersDeploymentOrder: null` (or `[null]`) in a project without code files deployed, and so did `TestFilePatterns` with a `null` item: such a `settings.json` fails now, hence `breaking: true`.

## Result
- `SettingsAccessor.CheckNoNulls` (`src/DbLive/Project/ProjectSettingsReader.cs`) runs after the deserialization of `settings.json`. It checks the five folder keys, `TestFilePatterns` and `CodeSubFoldersDeploymentOrder` and the items of both lists, and throws a `JsonException` with the key and the JSON path; the `catch` around it wraps it in `InvalidProjectSettingsException`, as it wraps the errors of System.Text.Json. So the inner exception stays a `JsonException`, as the docs and the XML doc of the exception say. The keys are listed by hand: a new key of a reference type needs a line there (the summary of the method says so).
- Tests: nine cases of `SettingsTests.LoadSettings_InvalidFile_ErrorNamesFile`, `null` for each folder key and each list, and a `null` item of each list. All nine failed before the change ("No exception was thrown", verified) and pass after; all 592 tests of `DbLive.Tests` pass, and `DbLive` builds without warnings on net8.0, net9.0 and net10.0.
- Docs: `docs/llms-full.txt` §9.1 (`null` is an error for every key but `ProjectId` and for a list item, an empty list is `[]`, the message for `null`) and the row of `InvalidProjectSettingsException`; `docs/project/configuration.md` (file format).
