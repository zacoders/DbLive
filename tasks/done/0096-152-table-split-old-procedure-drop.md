---
type: docs
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# `docs/examples/table-split.md`: the old procedure is dropped by "breaking migrations"

## Release note
None. A correction of `docs/examples/table-split.md`; DbLive behaves as before.

## Problem
`docs/examples/table-split.md:18-20`, step 4: "you can safely run breaking migrations to clean up the legacy state: The legacy `GetPerson_v1` procedure is dropped." An old version of a procedure is dropped by a breaking script of the Code folder next to its file (`Code/.../GetPerson.breaking.sql`), not by a migration breaking script: `docs/llms-full.txt` §6.1, §18.5 step 3, §18.6 ("do not create a migration version only to carry the drop"), `docs/deployment/breaking-changes.md` ("Dropping an old procedure version"). The demo has no `GetPerson` procedures (`table-split.md:69`); its `007.breaking.sql` drops only the trigger and the column.

## Docs to update
- `docs/examples/table-split.md` step 4: "the breaking phase": `GetPerson_v1` by a breaking script next to its file, the trigger and the column by `007.breaking.sql`; in that order (§6.1: Code breaking scripts run first)

## Done when
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.

## Result
Docs only, no change in behavior. `docs/examples/table-split.md`, step 4: "the breaking phase", in its order. First `GetPerson_v1` is dropped by a breaking script of the Code folder next to its file (`Code/dbo/GetPerson.breaking.sql`, linked to "Dropping an old procedure version" of `docs/deployment/breaking-changes.md`), as these scripts run before the breaking scripts of migrations; then `007.breaking.sql` drops the trigger and the `PhoneNumber` column. `mkdocs build --strict` passes.
