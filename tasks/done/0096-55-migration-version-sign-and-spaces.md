---
type: bug
priority: low
created: 2026-11-07
closed: 2026-11-07
---
# The version of a migration accepts a sign and spaces

## Release note
A migration file whose version has a sign or a space (`+001.migration.sql`, `-5.migration.sql`, ` 001.migration.sql`) fails with `InvalidMigrationVersionException`; it was read as a number, so `+001.migration.sql` was version 1 next to `001.migration.sql`. The version is read the same way in every culture. The message of `InvalidMigrationVersionException` now names the file and the reason: `Invalid migration version in file '+001.migration.sql'. The version part '+001' must be digits only.`

## Problem
`MigrationFileNameParser` read the version with `long.TryParse(string, out long)`: the current culture and `NumberStyles.Integer`, which allow a sign and leading and trailing spaces, and take the minus sign of the culture. The docs say that file names are read the same way in every culture (0096-50 fixed only the lower-casing).

The message of `InvalidMigrationVersionException` was `Cannot parse file '<file>', and version part '<reason>'.`: every caller passed the reason where the constructor expected the version part.

Found by the code review of 2026-11-07 (finding 10).

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs:34`
- `src/DbLive/Project/Exceptions/InvalidMigrationVersionException.cs`

## Fix
`long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out long version)`: digits only. The exception message takes the reason as a reason.

## Docs to update
- `docs/llms-full.txt` §5.1 (`<version>`, the errors table), §17
- `docs/project/structure.md` ("Version"), `docs/deployment/migrations.md` (errors)

## Done when
- [x] Fixed, with a unit test
- [x] Docs updated

## Result
- Digits only, invariant culture. The property `InvalidMigrationVersionException.MigrationVersionStr` keeps its name (public API) and now documents that it holds the reason, as it always did.
- Tests: `MigrationFileNameParserTests.Throws_When_Version_Is_Not_Only_Digits` (`+001`, `-5`, a leading and a trailing space) and `Invalid_Version_Message_Names_The_File_And_The_Version_Part`.
