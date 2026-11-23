---
type: bug
priority: low
created: 2026-11-23
---
# A `ProjectId` longer than 128 characters is caught after the database is created

## Problem
§12 step 1 of `docs/llms-full.txt` (line 853) says that an error in `settings.json` fails the deployment when the project is loaded, before anything touches the server, and creates no database. The XML doc of `IDbLive.DeployAsync` and the release note of 0096-65 say the same.

The length of `ProjectId` is checked in `ProjectIdValidator.GetSettingsProjectIdAsync` (`src/DbLive/Deployers/ProjectIdValidator.cs:99-103`). It runs in `DbLive.DeployAsync` at line 48, after `CreateDBAsync(true)` (line 33) and after the deployment lock (line 43):
- with `CreateDbIfNotExists = true` the deployment leaves an empty database;
- without it, a missing database fails with `DatabaseNotFoundException` instead of the error in `settings.json`.

It is the only check of a `settings.json` value that runs after the server is touched. The exception type does not fit either: `ProjectIdMismatchException`, "ProjectId exceeds maximum length of 128 characters.", without the file name.

## Fix
Check the length when `settings.json` is read (`SettingsAccessor.LoadSettingsAsync` or `ProjectLoader`) and throw `InvalidProjectSettingsException`, which names the file. `RunAllTestsAsync` and the `[SqlFact]` discovery then fail the same way.

## Docs to update
- `docs/llms-full.txt` §14 (line 1000), §17 (row `ProjectIdMismatchException`, and row `InvalidProjectSettingsException`)
- `docs/features/project-id.md`
- the XML doc of `ProjectIdMismatchException`

## Done when
- [ ] Fixed, with a test: a `ProjectId` of 129 characters fails with `InvalidProjectSettingsException` and creates no database
- [ ] Docs updated
