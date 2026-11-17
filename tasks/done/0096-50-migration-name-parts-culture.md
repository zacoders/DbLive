---
type: bug
priority: low
created: 2026-11-07
closed: 2026-11-07
---
# Upper-case parts of migration file names depend on the culture

## Release note
Upper-case type and settings parts of migration file names are now recognized in every culture. In the Turkish and Azerbaijani cultures, where `I` lower-cases to the dotless `ı`, `001.BREAKING.sql` was read as a migration named `BREAKING`, and `001.MIGRATION.SETTINGS.json` failed with `UnknownMigrationSettingsException`.

## Problem
`MigrationFileNameParser` lower-cased the type part and the settings part with `string.ToLower()`, which uses the current culture. The docs say the parts are matched ignoring case on every OS. In `tr-TR` `"BREAKING".ToLower()` is `"breakıng"`, so (verified by the new unit test on the old code, 4 of 4 failed):
- `001.BREAKING.sql` was a migration named `BREAKING`, not a breaking script; next to `001.migration.sql` it failed with `DuplicateMigrationItemException`;
- `001.MIGRATION.sql` was a migration named `MIGRATION`;
- `001.MIGRATION.SETTINGS.json` and `001.BREAKING.SETTINGS.json` failed with `UnknownMigrationSettingsException`.

Names in lower or mixed case (`001.Breaking.sql`) were not affected: only the capital `I` lower-cases differently.

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs:20`, `:25`, `:93`
- `ToString().ToLower()` of `MigrationItemType`, `MigrationItemStatus`, `CodeItemType` and `CodeItemStatus` in `src/DbLive.MSSQL/MsSqlDA.cs` and `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: no value has a capital `I` today, so the stored values were right in every culture.

## Fix
- [x] `ToLowerInvariant()` in the parser and in both data access classes, and in `DbLiveTablesSnapshotTests`, which lists the stored values the same way.

## Docs to update
- `docs/llms-full.txt` §4: the parts of a migration file name are matched ignoring case "on every OS and in every culture".

## Done when
- [x] Fixed, with a unit test under `tr-TR`
- [x] Docs updated

## Notes
- Item 2 of 0096-45 (code review of 2026-11-05), in a task of its own so that its note goes under "Fixes" of the release notes: 0096-45 is breaking, and a task's note goes under one heading.
- The test checks that `"I".ToLower()` is `"ı"` under the culture it sets, so it cannot pass without the Turkish casing rules (for example with invariant globalization).

## Result
- `MigrationFileNameParser` and the data access classes of SQL Server and PostgreSQL lower-case with `ToLowerInvariant()`.
- Test: `MigrationFileNameParserTests.Parses_Upper_Case_Parts_In_Turkish_Culture` (`001.MIGRATION.sql`, `001.BREAKING.sql`, `001.MIGRATION.SETTINGS.json`, `001.BREAKING.SETTINGS.json` under `tr-TR`): 4 failed on the old code, 4 pass. All tests pass: DbLive.Tests 425, SQL Server 54, PostgreSQL 43, demo projects 21.
- Commits: the commit that closes 0096-45.
