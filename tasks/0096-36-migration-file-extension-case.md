---
type: bug
priority: low
created: 2026-11-03
---
# Migration file extensions are compared case-sensitively

## Problem
DbLive matches folders, file patterns and `settings.json` ignoring case, and so does the search for migration files (`*.sql`, `*.json`). The parser of migration file names then compares the extension case-sensitively, so a file that was found is rejected:

- `001.migration.SQL` → `InvalidMigrationItemTypeException` ("cannot have extension '.SQL'. Expected extension is '.sql'")
- `001.create-users.SQL` → `UnknownMigrationItemTypeException`

A code file `Code/dbo/V1.SQL` deploys fine. Verified 2026-11-03 (a console app on `SetScriptsFolder(...).CreateProject()`).

The docs now describe the current behavior: `docs/llms-full.txt` §4 and §5.1, `docs/project/structure.md` ("Extension"), `docs/deployment/migrations.md` (Errors).

## Where
- `src/DbLive/Project/MigrationFileNameParser.cs`: `Path.GetExtension(fileName)` compared with `==` to `.json` and `.sql`.

## Fix
- [ ] Compare the extension ignoring case. `UnknownMigrationItemTypeException` is then probably unreachable (any other extension is not read): remove it, or keep it as a guard.
- [ ] Test: `001.migration.SQL`, `001.name.Sql`, `001.m.s.JSON` are read like the lower-case names.

## Docs to update
- `docs/llms-full.txt` §4 ("one exception: the extension of a migration file") and §5.1 (errors table), §17 (`InvalidMigrationItemTypeException`, `UnknownMigrationItemTypeException`)
- `docs/project/structure.md` ("Extension"), `docs/deployment/migrations.md` (Errors)

## Done when
- [ ] Fixed, with a unit test
- [ ] Docs updated

## Notes
Found in the documentation review of #96 (2026-11-03).
