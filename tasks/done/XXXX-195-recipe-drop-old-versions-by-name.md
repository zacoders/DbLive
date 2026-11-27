---
type: docs
priority: low
created: 2026-11-27
closed: 2026-11-27
---
# Recipe: one breaking script that drops the old versions of code objects by name

## Release note
None. A new recipe in the docs; DbLive behaves as before.

## Problem
In a project with a versioned interface every contract change needs a drop of the old version in a breaking script next to the file (rule 14, §18.6). The drop is easy to forget, and a forgotten one leaves the old version in the database. The maintainer proposed one breaking script that finds the versions by name, `<name>_v<N>`, and drops every version but the latest.

## Decisions
- 2026-11-27, maintainer: a recipe that a project adds, not a part of DbLive.
- 2026-11-27, maintainer: several clients of the database are no limit for it: the breaking phase runs only when every client has moved to the new versions.
- 2026-11-27, maintainer: a revert to a lower version name is a mistake of the developer in a project that only moves forward; the recipe states the rule "the version number only grows".

## Notes
- The script takes the latest version from the database, not from the repository. A version that the database has and the repository does not makes it drop the version the application calls: a revert of the file to a lower name (`_v2` stays, because DbLive never drops code), or a branch deployed to a shared database. `[SqlDeployFact]` cannot find it: its new database never had the higher version. Hence the conditions of the recipe, and the notes in rule 14, §6, §18.8, `docs/deployment/code.md` and `docs/deployment/breaking-changes.md`, which describe the revert as the way to bring an old version back.
- Verified on 2026-11-27 with `sqlcmd` and `psql`, not deployed through DbLive, on SQL Server 2017 and 2025 and PostgreSQL 13 and 18. Each script ran twice; the second run dropped nothing.
  - Dropped: `_v1` and `_v2` next to `_v10` (compared as numbers); both overloads of `get_user_v1`; on PostgreSQL old views that read each other (one `drop view`) and a view that calls an old function (before the function); on SQL Server a view `with schemabinding` before its function, a procedure before its table type, a scalar `_v1` when `_v2` is a table-valued function.
  - Kept: other schemas; a procedure `_v1` next to a function `_v2`; `Get_v2_Users`, `Report_2024`, a single `Lonely_v3`; a quoted `"Mixed_V1"` on PostgreSQL; the functions of `uuid-ossp` in `public` (`uuid_generate_v1` to `uuid_generate_v5` match the pattern and belong to the extension).

## Docs to update
- `docs/llms-full.txt` rule 14, §6, §18.6, §18.8
- `docs/deployment/breaking-changes.md`, `docs/deployment/code.md`

## Done when
- [x] The recipe in §18.6 and in `docs/deployment/breaking-changes.md`, for both engines
- [x] The scripts verified on the oldest and the newest server version available
- [x] The places that describe a revert to a lower version name point to the recipe
- [x] `mkdocs build --strict` passes

## Result
The recipe "One breaking script for all old versions" at the end of §18.6 of `docs/llms-full.txt`, and "One script for all old versions" in `docs/deployment/breaking-changes.md` with a tab for each engine. Both give the script, the conditions (every interface object has `_v<N>` from its first version, the version number only grows, no database gets a version the repository lacks) and the limits (kinds, schemas, extensions, order of the drops, dependencies). Rule 14, §6, §18.8 and `docs/deployment/code.md` say that such a project brings an old contract back under the next number. No code changed.
