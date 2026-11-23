---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `DuplicateMigrationItemException` and `MigrationItemMustExistsException` do not name the files

## Release note
`DuplicateMigrationItemException` and `MigrationItemMustExistsException` name the files by their relative paths: `Files 'Migrations/001.migration.sql' and 'Migrations/Old/1.m.sql' are both the Migration item of migration version 1: ...` and `Migration version 5 has no migration script, only 'Migrations/005.breaking.sql'. ...`. Both exceptions have a new property `RelativePaths`. Nothing to change on upgrade.

## Problem
The messages are:
- "Migration item already defined, check sql migrations in the project. MigrationVersion='1', MigrationItemType=Migration." (`src/DbLive/Project/DbLiveProject.cs:158-161`)
- "Migration item must exists. MigrationVersion='5'." (`DbLiveProject.cs:171-174`)

Their properties (`MigrationVersion`, `MigrationItemType`) are internal, so a caller cannot read them either. Every other exception of the project load names its file (`RelativePath`); 0096-63 and 0096-90 fixed the same gap for folder scripts and `settings.json`.

For the case that §5.1 documents (`001.migration.sql` together with `Old/1.m.sql`), a user must search the tree for both files. Two branches merged with the same sequential version give this error too.

## Fix
- `DuplicateMigrationItemException`: both relative paths in the message and in a public `RelativePaths` (the first file is in `items[type].FileData.RelativePath`).
- `MigrationItemMustExistsException`: the files of the version that has no migration.
- Fix "must exists" on the way.

## Docs to update
- `docs/llms-full.txt` §5.1 (table of errors) and §17 (both rows)
- `docs/deployment/migrations.md` (Errors)
- `src/DbLive/PublicAPI.Shipped.txt` (new properties)

## Done when
- [x] Fixed, with unit tests of both messages
- [x] Docs updated

## Notes
- Only `RelativePaths` is public. `MigrationVersion` and `MigrationItemType` stay internal, as `MigrationVersion` of `MigrationSettingsWithoutScriptException`: the message has them, and a caller needs the files to find them.
- The files come in the order of their relative paths: `GetFolderFiles` sorts them, and the lookup by version keeps that order. So the first file of a duplicate is not always the older one, and the message does not say which file to renumber: DbLive does not know which one is applied. "Rename or delete one of them" fits both cases, two scripts (renumber the newer) and two settings files of one script (delete one).
- Three files of the same version and type: the exception names the first two, as before it stopped at the second.
- `MigrationItemMustExistsException` names every file of the version: its breaking script and settings files, also a lone `005.m.s.json`. The message repeats the hint of the XML doc and of §17: a breaking script of the Code folder drops an old version of a code object (the case of 0096-15).
- The class name `MigrationItemMustExistsException` stays: renaming a public type is a breaking change for a typo. Only the message is fixed.

## Result
- `DuplicateMigrationItemException(version, type, relativePath, otherRelativePath)`: message `Files '<path>' and '<path>' are both the <type> item of migration version N: a version has one file of each type. Rename or delete one of them.`, public `RelativePaths`. `DbLiveProject.GetMigrationsAsync` passes the path of the item already read and of the duplicate.
- `MigrationItemMustExistsException(version, relativePaths)`: message `Migration version N has no migration script, only '<path>', .... Add the migration script of this version, or fix the version in the names of these files. To drop an old version of a code object, use a breaking script of the Code folder instead.`, public `RelativePaths` with the files of the version.
- Two lines in `src/DbLive/PublicAPI.Shipped.txt`.
- Unit tests in `MigrationsTests`: `001.migration.sql` with `Old/1.m.sql` (message and `RelativePaths`), two settings files of one migration (`004.m.s.json`, `004.migration.settings.json`: type `MigrationSettings` in the message), a breaking script and a settings file of version 5 in two folders without a migration (message and `RelativePaths`). All 578 tests of `DbLive.Tests` pass; the Release build of `DbLive` has no warnings.
- Docs: `docs/llms-full.txt` §5.1 (both rows of the error table) and §17 (both rows: the message, the cause, `RelativePaths`), `docs/deployment/migrations.md` (Errors). No release note of an earlier task quotes the old messages.
