---
type: docs
priority: low
created: 2026-11-25
closed: 2026-11-25
---
# Release notes of 2.0 that name an internal option or a state inside the release

## Release note
None. The release notes of 2.0 are corrected; the packages do not change.

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
- `## Release note` of `tasks/done/0096-81-*.md`, `0096-120-*.md`, `0096-124-*.md`, `0096-129-*.md`, `0096-130-*.md`, `0096-30-02-*.md`
- `## Release note` of the tasks with findings of the check of all notes (see Result)

## Done when
- [x] 1–3 corrected, the preview checked
- [x] All notes of 2.0 checked against 1.5.0 the same way, findings corrected

## Notes
- Found by the code review of 2026-11-25.
- 2026-11-25, maintainer: correct the five notes, then check all notes of 2.0 against `1.5.0`.
- Checked against `1.5.0` (verified): `RecreateDatabase` is `internal` there; `DatabaseNotFoundException` and `MsSqlContainerFixtureBuilder` / `PostgreSqlContainerFixtureBuilder` do not exist there; a version that does not fit in a `long` failed with `Cannot parse file '...', and version part 'Version prefix failed to parse into a long integer.'`.
- 0096-120: the clause about a missing database is dropped, not rewritten: in 1.5 it failed with the provider's error, a corner case not worth a sentence. 0096-124: the comparison with the old message is dropped; 0096-55 already says that the message names the reason.
- 0096-81: the constructors `(project)` and `(project, image)` are already in the note of 0096-30-02 ("the second argument takes another one"); the sentence about a new major engine version in a default image moved there.
- The check of all notes: the preview (107 notes) in four parts, each note against `git show 1.5.0:...` / `git grep ... 1.5.0`, for five kinds of mistakes: an old state that is not 1.5.0; a fix of a bug that 1.5.0 did not have; an upgrade step for something new in 2.0; a name the user cannot see; a current state that a later task changed again. Every finding was checked again in the 1.5.0 code before the correction (verified by reading the code; the behavior of 1.5 was not run).
- Most findings are of one kind: a later task of 2.0 fixed what an earlier one of 2.0 had brought, and its note compared with that earlier state. Breaking scripts of the Code folder (new in 2.0) were named as having had bugs in five notes.
- The preview names `1.5.0...1.5.1` in the changelog link: without `--version` it takes the bump from the commits, and the PR title decides the version. Not a mistake of the notes.

## Result
Corrected `## Release note` sections, against 1.5.0:
- 0096-129, 0096-130: `RecreateDatabase` left out. 0096-120: no `DatabaseNotFoundException` as the 1.5 state; the database left by 1.5 had the `dblive` tables, not empty. 0096-124: no comparison with a 2.0 message. 0096-81: `DefaultImage` item dropped, the sentence about the default images moved to 0096-30-02; `IDbLiveDA` described as a whole with 0096-80.
- An old state that was a 2.0 state: 0096-04 (Arrange output counted in 1.5 too), 0096-07 (`init.sql` in `Code` was deployed as code in 1.5 too), 0096-80 (members of `IDbLiveDA`), 0096-119 (`ArgumentNullException`, not `NullReferenceException`; no match-all), 0096-65, 0096-72, 0096-59 (`Timeout` key), 0096-09 (`.gitattributes`), 0096-44 (guard of the breaking phase), 0096-109 and 0096-117 (docs written for 2.0), 0096-162 (`SetProject(null)`; `SetScriptsFolder` is new), 0096-177 (failed commit, isolation level in the log, `CodeSubFoldersDeploymentOrder` warning), 0096-155 (`Enlist=False`), 0096-87 (in 1.5 a code file ran without a transaction of DbLive, unless under `Deployment`).
- A fix of a bug that 1.5 did not have: 0096-164 and 0096-106 are `None.` now; parts dropped from 0096-36 (`001.m.s.JSON`; Linux did not find such files at all), 0096-50, 0096-64 (script files), 0096-142, 0096-169, 0096-156 (`ObsoleteMigrationSettingsFileException`), and the breaking scripts of the Code folder from 0096-51, 0096-60, 0096-141, 0096-177, 0096-181 (named in the present tense there).
- 0096-155 gets `breaking: true`: with `Enlist=False` DbLive 1.5 deployed and its SQL tests passed (without its transactions); now such a setup fails at the start. Its Notes said "Not breaking" against the 2.0 state before it.
- The preview builds; it no longer has the corrected names, and 0096-155 is under "Breaking changes".
