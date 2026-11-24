---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# `UnknownMigrationSettingsException` and `ObsoleteMigrationSettingsFileException` name the file without its folder

## Release note
`UnknownMigrationSettingsException` and `ObsoleteMigrationSettingsFileException` have a new property `RelativePath`, and their messages name the file by its relative path: `Unknown migration settings file Migrations/Old/004.custom.json. ...`. A file in a subfolder of `Migrations` is found by the message now. `ObsoleteMigrationSettingsFileException.FileName` stays, the name of the file without its folder. Nothing to change on upgrade.

## Problem
`MigrationFileNameParser` passes the file name, not the relative path, to these two exceptions (`MigrationFileNameParser.cs:49` and `:54`). `Migrations` is read with all its subfolders, so `Migrations/Old/004.custom.json` fails with "Unknown migration settings file 004.custom.json." and the user searches for it. Verified by the review on 2026-11-24. `UnknownMigrationSettingsException` has no property with the file; `ObsoleteMigrationSettingsFileException` has `FileName` only.

0096-124 and 0096-125 gave the other exceptions of the project load the relative path (`InvalidMigrationVersionException.RelativePath`, `DuplicateMigrationItemException.RelativePaths`) for this reason. The rows of §17 (lines 1108, 1112) promise no path, so it is consistency, not a mismatch.

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs:49`, `:54`
- `src/DbLive/Project/Exceptions/UnknownMigrationSettingsException.cs`, `ObsoleteMigrationSettingsFileException.cs`

## Fix
Pass the relative path; add `RelativePath` to both exceptions (public API: `PublicAPI.Shipped.txt`), name the file by it in the message.

- [x] Keep `ObsoleteMigrationSettingsFileException.FileName`, or replace it (no users yet)? Kept, computed from the relative path (see Notes).

## Docs to update
- `docs/llms-full.txt` §17 (both rows: the property)
- `docs/deployment/migrations.md` (Errors)

## Done when
- [x] Fixed, with unit tests for a file in a subfolder of `Migrations`
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- `FileName` of `ObsoleteMigrationSettingsFileException` stays, as `Path.GetFileName` of the relative path, the way 0096-124 kept `InvalidMigrationVersionException.FileName`: it is public, the maintainer kept the properties of the exceptions public (0096-81, 2026-11-14), and with both each property means one thing. Removing it would be a breaking change for nothing.
- `InvalidMigrationItemTypeException` and `UnknownMigrationItemTypeException` (internal) also get the file name from the parser (`MigrationFileNameParser.cs:70`, `:93`), but no file of a user project reaches them: `DbLiveProject.GetMigrationsAsync` reads only `*.sql` and `*.json` from `Migrations`, a `.json` file goes to the settings branch, and a `.sql` file with a type part is a script. Left as they are.

## Result
- `MigrationFileNameParser` passes the relative path to `ObsoleteMigrationSettingsFileException` and `UnknownMigrationSettingsException`. Both have `RelativePath`, and their messages name the file by it; `ObsoleteMigrationSettingsFileException.FileName` is the name of the file without its folder, as before. Two new lines in `src/DbLive/PublicAPI.Shipped.txt`.
- Unit tests in `MigrationFileNameParserTests`: `Migrations/Old/004.settings.json` (message, `RelativePath`, `FileName`) and `Migrations/Old/004.custom.json` (message, `RelativePath`). All 682 tests of `DbLive.Tests` pass; the Release build of `DbLive` has no warnings.
- Docs: `docs/llms-full.txt` §5.1 (the table of errors raised while reading the project: the messages name the file) and §17 (both rows: the message, `RelativePath`, `FileName`, subfolders, "Nothing ran"), `docs/deployment/migrations.md` (Errors).
