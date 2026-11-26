---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-24
breaking: true
---
# A file in two project folders: `CodeFolder: ""` or a project folder inside another one

## Release note
**A file in two project folders stops the deployment.** A project folder inside another one (`"TestsFolder": "Code/Tests"`), two folder settings that name one folder, or `"CodeFolder": ""` next to files in `Migrations`, `Tests`, `BeforeDeploy` or `AfterDeploy` made DbLive read such a file twice: a migration ran a second time as code, a test of `Tests` was deployed as code and its changes stayed in the database, and a test found twice failed with `ArgumentException`. Now the deployment fails with `FilesInSeveralProjectFoldersException` before anything is deployed; the message names the files and the folder settings each one is in. `[SqlFact]` discovery and `IDbLiveTester` fail the same way. To upgrade: set the folders in `settings.json` apart, or move the files; keep `"CodeFolder": ""` only for a project of code files.

## Problem
The folder keys of `settings.json` may name folders inside each other. `CodeFolder: ""` is one such case, and a supported one: the scripts root is the Code folder (`ProjectFoldersTests.Root_Folder_Contains_All_Files`), which works for a project of code files only. When the other project folders have files too, those files are read twice: as their own kind and as code. Verified on 2026-11-23 with a temporary unit test of `DbLiveProject` (`CodeFolder: ""`, the other folders at their defaults):

| File | Read as |
|---|---|
| `Migrations/001.migration.sql`, `Migrations/002.m.sql` | migration and code file |
| `Migrations/001.breaking.sql`, `Migrations/002.b.sql` | migration breaking script and breaking script of the Code folder |
| `BeforeDeploy/before.sql`, `AfterDeploy/after.sql` | BeforeDeploy/AfterDeploy script and code file |
| `Tests/select.sql` | test and code file |
| `Tests/x.test.sql` | test, twice |

So a deployment runs every migration a second time as code, the BeforeDeploy and AfterDeploy scripts as code too, and the breaking phase runs each migration breaking script also as a breaking script of the Code folder. Read in the code, not run: the test found twice makes `DbLiveTester.LoadTestsAsync` (`ToDictionary`) throw `ArgumentException` at the first `RunTestAsync`, and `RunAllTestsAsync` runs it twice. A project folder inside another one (`TestsFolder: "Code/Tests"`) reads files twice the same way.

The check of files outside the project folders (§4) never fails with `CodeFolder: ""`, so nothing stops such a project. Since 0096-122 the docs describe this and advise `""` only for a project of code files.

## Where
- `src/DbLive/Project/DbLiveProject.cs`: `IsInFolder` (true for every path when the folder is `""`), `GetCodeGroupsAsync`, `GetCodeBreakingItemsAsync`, `GetTestsAsync` (the tests of `TestsFolder` and the tests by `TestFilePatterns` in `CodeFolder`).

## Fix
Fail every read of the project when a file is in more than one project folder, with an exception that names the file and the folders, like the other checks of §4: the deployment stops when it loads the project, `[SqlFact]` discovery and `IDbLiveTester` fail the same way. A check of the files, not of the settings alone: the settings alone would reject `CodeFolder: ""` in every project, since the other folders always have names, also when they have no files.

- [x] Which exception: a new one (`RelativePath`, the folders), or `InvalidProjectSettingsException` on `settings.json`? Maintainer, 2026-11-24: a new one.

## Docs to update
- `docs/llms-full.txt` §4 (the sentence on `CodeFolder: ""`) and §17
- `docs/project/configuration.md` (folder keys) or `docs/project/structure.md`

## Done when
- [x] Fixed, with unit tests: `CodeFolder: ""` with a migration, a test and a BeforeDeploy script fails; `CodeFolder: ""` with code files only still works; `TestsFolder: "Code/Tests"` with a test fails
- [x] Docs updated

## Notes
- Found while closing 0096-122. The maintainer decided on 2026-11-23 to close 0096-122 as a docs fix and to keep the check in a task of its own.
- The maintainer chose on 2026-11-24 the check of the files (not "the innermost folder wins", not a check of the settings alone, not docs only) and a new exception.
- Real but narrow: it needs a folder setting other than the default. Most cases are loud (a migration run again as code fails on `create table`, a test found twice throws), but a test of `Tests/` deployed as code is silent: tests check result sets, so the script runs without an assertion and its changes are committed. Nested project folders were not forbidden in the docs.
- Hidden files are skipped, like in the check of files outside the project folders: an empty `Migrations/.gitkeep` next to `CodeFolder: ""` would otherwise fail a project of code files. So `Migrations/.old/x.sql` with `CodeFolder: ""` still reads twice; not worth a rule of its own.
- The message lists the folders of each file as settings (`CodeFolder: "", TestsFolder: "Tests"`), so `""` is visible and the user sees which key to change.

## Result
- `DbLiveProject.ReadFiles`, after the check of files outside the project folders: a file that is not hidden and is in more than one project folder fails every read with `FilesInSeveralProjectFoldersException` (count, the first 5 files by relative path, each with its folder settings). It covers a project folder inside another one, two settings with one folder, and `CodeFolder: ""` next to files of the other folders; a nested folder without files passes. `NamedOutsideFiles` is `NamedFiles` now, used by both exceptions; `IsHidden` is shared by both checks.
- `FilesInSeveralProjectFoldersTests`: `CodeFolder: ""` with a migration, a test and a BeforeDeploy script fails every read; `CodeFolder: ""` with code files, a test of `Code` and hidden files in `Migrations/` and `Tests/` works; `TestsFolder: "Code/Tests"` (also with `\` and another case) fails; a nested folder without files works; two settings with one folder fail; the message names the first 5 files.
- `dotnet test DbLive.sln`: 905 passed, 0 failed (unit tests, SQL Server and PostgreSQL integration tests, all demos).
- Docs: `docs/llms-full.txt` rule 16, §4, §9, §12 (step 1), §17; `docs/project/configuration.md` (folder keys).
