---
type: docs
priority: low
created: 2026-11-25
---
# Release notes of 2.0 that name an internal option or a state inside the release

## Problem
The notes of a release describe the change from the previous release (AGENTS.md, `tasks/README.md`). A few notes of 2.0 do not:

1. **An internal option.** `tasks/done/0096-129-database-name-not-escaped.md:10` and `tasks/done/0096-130-postgresql-create-drop-database-timeout.md:10` name `RecreateDatabase` ("SQL Server creates it (`CreateDbIfNotExists`, `RecreateDatabase`)", "creating the database (`CreateDbIfNotExists`, `RecreateDatabase`)"). `DeployParameters.RecreateDatabase` is `internal` (`src/DbLive/DeployParameters.cs:40`), also in 1.5.0; §12 step 2 calls it "(internal option)".
2. **A state inside the release as "1.5" or "before".**
   - 0096-120: "... and a missing database failed with `DatabaseNotFoundException` instead". `DatabaseNotFoundException` came with 0096-35 in this release (`git grep DatabaseNotFoundException 1.5.0` finds nothing); in 1.5 `CreateDbIfNotExists` was on by default, and a missing database failed with SqlClient error 4060.
   - 0096-124: a too large version now fails "instead of saying that the version is not digits only". That wording came with 0096-55 in this release; 1.5.0 said `Cannot parse file '...', and version part '...'.`
3. **An upgrade step for types new in 2.0.** `tasks/done/0096-81-public-members-not-needed.md:19` (Breaking changes): "`DefaultImage` of `MsSqlContainerFixtureBuilder` and `PostgreSqlContainerFixtureBuilder` is internal; ...". Both classes and their packages are new in 2.0: no user of 1.5 had `DefaultImage`.

## Fix
Correct the `## Release note` sections: leave out `RecreateDatabase`; describe 1.5 as it was (0096-120, 0096-124); drop the `DefaultImage` item of 0096-81 (or keep the constructors as a feature of the new packages elsewhere). Check the preview: `dotnet run .github/scripts/release-notes.cs`.

## Docs to update
- `## Release note` of `tasks/done/0096-81-*.md`, `0096-120-*.md`, `0096-124-*.md`, `0096-129-*.md`, `0096-130-*.md`

## Done when
- [ ] 1–3 corrected, the preview checked

## Notes
- Found by the code review of 2026-11-25.
