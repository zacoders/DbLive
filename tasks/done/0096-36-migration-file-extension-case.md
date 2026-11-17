---
type: bug
priority: low
created: 2026-11-03
closed: 2026-11-03
---
# Migration file extensions are compared case-sensitively

## Release note
Migration files whose extension is not in lower case (`001.migration.SQL`, `001.users.Sql`, `001.m.s.JSON`) are now read like `.sql` / `.json` files. Before, DbLive found them but stopped the deployment with `InvalidMigrationItemTypeException` or `UnknownMigrationItemTypeException`. Nothing to change on upgrade.

## Problem
DbLive matches folders, file patterns and `settings.json` ignoring case, and so does the search for migration files (`*.sql`, `*.json`). The parser of migration file names then compares the extension case-sensitively, so a file that was found is rejected:

- `001.migration.SQL` → `InvalidMigrationItemTypeException` ("cannot have extension '.SQL'. Expected extension is '.sql'")
- `001.create-users.SQL` → `UnknownMigrationItemTypeException`

A code file `Code/dbo/V1.SQL` deploys fine. Verified 2026-11-03 (a console app on `SetScriptsFolder(...).CreateProject()`).

The docs now describe the current behavior: `docs/llms-full.txt` §4 and §5.1, `docs/project/structure.md` ("Extension"), `docs/deployment/migrations.md` (Errors).

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs`: `Path.GetExtension(fileName)` compared with `==` to `.json` and `.sql`.

## Fix
- [x] Compare the extension ignoring case. `UnknownMigrationItemTypeException` is then probably unreachable (any other extension is not read): remove it, or keep it as a guard.
- [x] Test: `001.migration.SQL`, `001.name.Sql`, `001.m.s.JSON` are read like the lower-case names.

## Docs to update
- `docs/llms-full.txt` §4 ("one exception: the extension of a migration file") and §5.1 (errors table), §17 (`InvalidMigrationItemTypeException`, `UnknownMigrationItemTypeException`)
- `docs/project/structure.md` ("Extension"), `docs/deployment/migrations.md` (Errors)

## Done when
- [x] Fixed, with a unit test
- [x] Docs updated

## Notes
Found in the documentation review of #96 (2026-11-03).

- Maintainer decision 2026-11-03: compare the extension ignoring case in the parser (the other options were a clearer error for a non-lower-case extension, or no change).
- After the fix both exceptions, not only `UnknownMigrationItemTypeException`, are unreachable from a user project: `DbLiveProject` passes only `*.sql` / `*.json` files (in any case) to the parser, and both now take a valid branch. They are kept as a guard: they are public types (removing them would be an API change), and the parser also reads DbLive's own embedded scripts (`InternalDbLiveProject`) without a pattern filter. The `.txt` tests of the parser still cover them.

## Result
- `MigrationFileNameParser` compares the extension with `StringComparison.OrdinalIgnoreCase` (`IsExtension`), like the file patterns that found the file. The type and settings tokens were already case-insensitive.
- `InvalidMigrationItemTypeException` and `UnknownMigrationItemTypeException` stay as a guard (see Notes).
- Tests: `MigrationFileNameParserTests.Parses_Extension_Ignoring_Case` (`001.migration.SQL`, `001.U.Sql`, `001.name.Sql`, `001.m.s.JSON`, `001.B.Settings.Json`) and `MigrationsTests.GetMigrations_Reads_Extensions_Ignoring_Case` (the whole path through `DbLiveProject`: pattern filter, parser, item settings). `DbLive.Tests`: 403 passed.
- Docs: `docs/llms-full.txt` §4 (the exception for the migration extension removed), §5.1 and §17 (the error rows removed); `docs/project/structure.md` ("Extension": any case); `docs/deployment/migrations.md` (the error row removed).
