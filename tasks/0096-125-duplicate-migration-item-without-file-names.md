---
type: bug
priority: low
created: 2026-11-23
---
# `DuplicateMigrationItemException` and `MigrationItemMustExistsException` do not name the files

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
- `src/DbLive/PublicAPI.Shipped.txt` (new properties)

## Done when
- [ ] Fixed, with unit tests of both messages
- [ ] Docs updated
