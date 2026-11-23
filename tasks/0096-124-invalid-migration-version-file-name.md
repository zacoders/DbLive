---
type: bug
priority: low
created: 2026-11-23
---
# `InvalidMigrationVersionException.FileName` is a file name or a path, and one message is wrong

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
- [ ] Fixed, with unit tests for both paths and for a version of 20 digits
