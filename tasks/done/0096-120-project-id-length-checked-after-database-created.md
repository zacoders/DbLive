---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# A `ProjectId` longer than 128 characters is caught after the database is created

## Release note
**A `ProjectId` longer than 128 characters is an error of `settings.json`.** DbLive 1.5 failed the deployment with `ProjectIdMismatchException` after it had created the database (with `CreateDbIfNotExists` a new database with only the `dblive` tables was left). Now the deployment fails when it loads the project, before anything touches the server, with `InvalidProjectSettingsException`, as for the other errors of `settings.json`: `Invalid project settings file settings.json: The value of 'ProjectId' cannot be longer than 128 characters. Path: $.ProjectId.` `RunAllTestsAsync` and the `[SqlFact]` tests fail the same way.

## Problem
§12 step 1 of `docs/llms-full.txt` (line 853) says that an error in `settings.json` fails the deployment when the project is loaded, before anything touches the server, and creates no database. The XML doc of `IDbLive.DeployAsync` and the release note of 0096-65 say the same.

The length of `ProjectId` is checked in `ProjectIdValidator.GetSettingsProjectIdAsync` (`src/DbLive/Deployers/ProjectIdValidator.cs:99-103`). It runs in `DbLive.DeployAsync` at line 48, after `CreateDBAsync(true)` (line 33) and after the deployment lock (line 43):
- with `CreateDbIfNotExists = true` the deployment leaves an empty database;
- without it, a missing database fails with `DatabaseNotFoundException` instead of the error in `settings.json`.

It is the only check of a `settings.json` value that runs after the server is touched. The exception type does not fit either: `ProjectIdMismatchException`, "ProjectId exceeds maximum length of 128 characters.", without the file name.

## Fix
Check the length when `settings.json` is read (`SettingsAccessor.LoadSettingsAsync` or `ProjectLoader`) and throw `InvalidProjectSettingsException`, which names the file. `RunAllTestsAsync` and the `[SqlFact]` discovery then fail the same way.

## Docs to update
- `docs/llms-full.txt` §9.1, §14 (line 1000), §17 (row `ProjectIdMismatchException`, and row `InvalidProjectSettingsException`)
- `docs/features/project-id.md`
- `docs/project/configuration.md` (file format)
- the XML doc of `ProjectIdMismatchException`

## Done when
- [x] Fixed, with a test: a `ProjectId` of 129 characters fails with `InvalidProjectSettingsException` and creates no database
- [x] Docs updated

## Notes
- It takes a mistake in `settings.json` to get here, and the result was harmless (an empty database, a clear message). It was fixed because the docs promise that an error of `settings.json` touches nothing, and the fix is smaller than an exception to that promise in the docs.
- `SettingsAccessor` and not `ProjectLoader`: `DbLiveTester` does not use `ProjectLoader`, while every path reads the settings through `SettingsAccessor`.
- DbLive 1.5.0 had the same check in `ProjectIdValidator`, with `ProjectIdMismatchException` (read in the tag `1.5.0`).
- `RunAllTestsAsync` reads the server before `settings.json` (`CheckDatabaseExistsAsync`, `DbLiveInstalledAsync`, the required DbLive version), as before: the tests write nothing, so only the type of the exception changes there.
- A whitespace-only `ProjectId` means no ID; one of more than 128 characters is now an error too. Not worth a special case.

## Result
- `SettingsAccessor.LoadSettingsAsync` (`src/DbLive/Project/ProjectSettingsReader.cs`) checks the length of `ProjectId` after `CheckNoNulls` and throws a `JsonException` with the path `$.ProjectId`, which the `catch` wraps in `InvalidProjectSettingsException`, as it does for `null`. The limit is `ProjectIdTools.MaxLength` (`src/DbLive/Common/ProjectIdTools.cs`), the length of `dblive.version.project_id` on both providers. `ProjectIdValidator` lost its check and `MaxProjectIdLength`; the XML doc of `ProjectIdMismatchException` no longer names the length.
- Tests: `SettingsTests.LoadSettings_ProjectIdOfMaxLength` (128 characters load) and `LoadSettings_ProjectIdTooLong_ErrorNamesFile` (129 characters, the full message); `ProjectIdValidatorTests.ValidateAsync_throws_when_project_id_exceeds_max_length` removed. `InvalidProjectTests.Project_id_longer_than_128_characters_fails_the_deployment_before_the_database_is_created` on SQL Server and PostgreSQL: with `CreateDbIfNotExists`, `InvalidProjectSettingsException` and no database. Both failed on the old code with `ProjectIdMismatchException` (verified), and pass after the change. All 593 tests of `DbLive.Tests` pass, and the `InvalidProjectTests` and `ProjectIdTests` of both providers.
- Docs: `docs/llms-full.txt` §9.1 (the error and its message), §14, §17 (rows `InvalidProjectSettingsException` and `ProjectIdMismatchException`); `docs/features/project-id.md` (Format); `docs/project/configuration.md` (file format).
