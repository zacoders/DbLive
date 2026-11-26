---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `InvalidMigrationVersionException.FileName` is a file name or a path, and one message is wrong

## Release note
`InvalidMigrationVersionException` has a new property `RelativePath`, and its message names the file by its relative path: `Invalid migration version in file 'Migrations/+001.migration.sql'. ...`. `FileName` is always the name of the file without its folder (with `EnforceTimestampVersion` it held the path). A migration version of digits only that is larger than 9223372036854775807 fails with `The version '...' is larger than 9223372036854775807.` Nothing to change on upgrade.

## Problem
- The XML doc of `InvalidMigrationVersionException.FileName` says "The name of the file, without its folder". `MigrationFileNameParser` passes `Path.GetFileName` (`src/DbLive/Project/MigrationFileNameParser.cs:39`), but `DbLiveProject` passes the relative path to `MigrationVersionValidator.Validate` (`src/DbLive/Project/DbLiveProject.cs:145`), which puts it into `FileName` and the message. So with `EnforceTimestampVersion` the property holds `Migrations/001.migration.sql`, otherwise `001.migration.sql`.
- A version of digits only that does not fit in a `long` (20 digits or more) fails with "The version part '...' must be digits only." (`MigrationFileNameParser.cs:36-39`), which is wrong for that file.

The sibling exceptions of the project load have `RelativePath`.

## Fix
Add `RelativePath` and pass the same value from both places (or keep `FileName` and pass the file name from both). Give the overflow its own message: "The version '...' is larger than 9223372036854775807."

## Docs to update
- `docs/llms-full.txt` §17 (row `InvalidMigrationVersionException`), if a property is added
- `src/DbLive/PublicAPI.Shipped.txt` (a new property)

## Done when
- [x] Fixed, with unit tests for both paths and for a version of 20 digits

## Notes
- Both: `RelativePath` is added, and `FileName` stays, computed from the relative path. `FileName` is public since 1.x, and the maintainer kept the properties of the exceptions public (0096-81, 2026-11-14); with both, each property means one thing.
- The message names the relative path, as `UndoNotSupportedException` and `InvalidMigrationItemSettingsException` do: the Migrations folder is read with its subfolders (`Migrations/_Old/001.migration.sql`), so a file name alone may not find the file. The parser case named the file name before, the timestamp case the path.
- The overflow is told apart by the version part: not empty and only ASCII digits (`char.IsAsciiDigit`), because `long.TryParse` with `NumberStyles.None` takes nothing else. Full-width digits (`１２`) stay "must be digits only". It is not only 20 digits: `9223372036854775808` has 19. Leading zeros still count for nothing: `000000000000000000001` (21 digits) is version 1, as before.
- The release note of 0096-55 quoted the message with the file name; it now quotes it with the relative path.

## Result
- `InvalidMigrationVersionException` takes the relative path: `RelativePath` holds it, `FileName` is `Path.GetFileName` of it, the message names the relative path. `MigrationFileNameParser` passes the relative path instead of the file name; the parameter of `IMigrationVersionValidator.Validate` is renamed to `relativePath`. New line in `src/DbLive/PublicAPI.Shipped.txt`.
- `MigrationFileNameParser`: a version part of ASCII digits that does not fit in a `long` gives `The version '<part>' is larger than 9223372036854775807.`
- Unit tests: `MigrationFileNameParserTests` (message, `RelativePath` and `FileName` of `Migrations/_Old/+001.migration.sql`; 20 digits and `long.MaxValue + 1` with the new message; `long.MaxValue` and 21 digits with leading zeros parse; an empty and a full-width version part are not digits), `MigrationVersionValidatorTests` (the same `RelativePath` and `FileName` for `Migrations/_Old/001.migration.sql`). `MigrationsTests` already checks that `DbLiveProject` passes the relative path to the validator. All 577 tests of `DbLive.Tests` pass; the Release build of `DbLive` has no warnings.
- Docs: `docs/llms-full.txt` §5.1 (the limit of the version, a row for the overflow) and §17 (row `InvalidMigrationVersionException`: the message, `RelativePath`, `FileName`), `docs/deployment/migrations.md` (Errors), `docs/project/structure.md` (Version); the release note of `tasks/done/0096-55-migration-version-sign-and-spaces.md`.
