---
type: bug
priority: low
created: 2026-11-23
---
# A `null` value in `settings.json` fails with `NullReferenceException`

## Problem
It takes a mistake in `settings.json` to get here, but the docs promise a clear error for it: `docs/llms-full.txt` §9.1 (line 637) and `docs/project/configuration.md:32` say that an error in the file ("a wrong value") fails with `InvalidProjectSettingsException`, which names the file and the key.

`"MigrationsFolder": null` (or another folder key), `"TestFilePatterns": null` and `"CodeSubFoldersDeploymentOrder": null` deserialize without an error and store `null`, because the net8.0 System.Text.Json does not enforce nullable annotations. The project load then fails with a bare `NullReferenceException` that names neither the file nor the key:
- a folder: `ToFolder(null)` (`src/DbLive/Project/DbLiveProject.cs:396`);
- `TestFilePatterns`: `DbLiveProject.cs:47`;
- `CodeSubFoldersDeploymentOrder`: the `foreach` at `DbLiveProject.cs:57`, when the project has code files.

It still fails before anything touches the server. The deserialization was verified by experiment on 2026-11-23; the exceptions by reading the code.

## Fix
After `GetSettings<DbLiveSettings>` in `SettingsAccessor.LoadSettingsAsync` (`src/DbLive/Project/ProjectSettingsReader.cs:55`), check the reference-type properties for `null` and throw `InvalidProjectSettingsException(settingsPath, "The value of 'X' cannot be null.", ...)`. `RespectNullableAnnotations` exists only from System.Text.Json 9.

## Done when
- [ ] Fixed, with unit tests for a folder key, `TestFilePatterns` and `CodeSubFoldersDeploymentOrder` set to `null`
