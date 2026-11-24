---
type: bug
priority: low
created: 2026-11-24
---
# `UnknownMigrationSettingsException` and `ObsoleteMigrationSettingsFileException` name the file without its folder

## Problem
`MigrationFileNameParser` passes the file name, not the relative path, to these two exceptions (`MigrationFileNameParser.cs:49` and `:54`). `Migrations` is read with all its subfolders, so `Migrations/Old/004.custom.json` fails with "Unknown migration settings file 004.custom.json." and the user searches for it. Verified by the review on 2026-11-24. `UnknownMigrationSettingsException` has no property with the file; `ObsoleteMigrationSettingsFileException` has `FileName` only.

0096-124 and 0096-125 gave the other exceptions of the project load the relative path (`InvalidMigrationVersionException.RelativePath`, `DuplicateMigrationItemException.RelativePaths`) for this reason. The rows of §17 (lines 1108, 1112) promise no path, so it is consistency, not a mismatch.

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs:49`, `:54`
- `src/DbLive/Project/Exceptions/UnknownMigrationSettingsException.cs`, `ObsoleteMigrationSettingsFileException.cs`

## Fix
Pass the relative path; add `RelativePath` to both exceptions (public API: `PublicAPI.Shipped.txt`), name the file by it in the message. Keep `ObsoleteMigrationSettingsFileException.FileName`, or replace it (no users yet)?

## Docs to update
- `docs/llms-full.txt` §17 (both rows: the property)

## Done when
- [ ] Fixed, with unit tests for a file in a subfolder of `Migrations`
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
