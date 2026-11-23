---
type: bug
priority: low
created: 2026-11-23
---
# A file in two project folders: `CodeFolder: ""` or a project folder inside another one

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
- `src/DbLive/Project/DbLiveProject.cs`: `IsInFolder` (`:403-407`, true for every path when the folder is `""`), `GetCodeGroupsAsync`, `GetCodeBreakingItemsAsync`, `GetTestsAsync` (`:207-211`, the tests of `TestsFolder` and the tests by `TestFilePatterns` in `CodeFolder`).

## Fix
Fail every read of the project when a file is in more than one project folder, with an exception that names the file and the folders, like the other checks of §4: the deployment stops when it loads the project, `[SqlFact]` discovery and `IDbLiveTester` fail the same way. A check of the files, not of the settings alone: the settings alone would reject `CodeFolder: ""` in every project, since the other folders always have names, also when they have no files.

- [ ] Which exception: a new one (`RelativePath`, the folders), or `InvalidProjectSettingsException` on `settings.json`?

## Docs to update
- `docs/llms-full.txt` §4 (the sentence on `CodeFolder: ""`) and §17
- `docs/project/configuration.md` (folder keys) or `docs/project/structure.md`

## Done when
- [ ] Fixed, with unit tests: `CodeFolder: ""` with a migration, a test and a BeforeDeploy script fails; `CodeFolder: ""` with code files only still works; `TestsFolder: "Code/Tests"` with a test fails
- [ ] Docs updated

## Notes
- Found while closing 0096-122. The maintainer decided on 2026-11-23 to close 0096-122 as a docs fix and to keep the check in a task of its own.
