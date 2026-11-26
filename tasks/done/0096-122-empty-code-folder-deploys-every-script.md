---
type: docs
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `CodeFolder: ""` is described as a way past the check of files outside the project folders

## Release note
None. A correction of `docs/llms-full.txt`; DbLive behaves as before.

## Problem
`docs/llms-full.txt` §4 (line 212): "With `CodeFolder: ""` every file is in the Code folder, so the check never fails." It reads like a safe setting.

`IsInFolder(path, "")` is always true (`src/DbLive/Project/DbLiveProject.cs:403-404`), so every `*.sql` file of the project becomes a code file:
- migrations run a second time as code;
- tests in `Tests/` that do not match `TestFilePatterns` are deployed as code, outside a rolled-back transaction;
- BeforeDeploy and AfterDeploy scripts run as code too.

0096-33 noted this ("everything is code"); the docs do not say it. An agent that meets `FilesOutsideProjectFoldersException` can take the sentence as the fix.

## Fix
Replace the sentence: "`CodeFolder: ""` makes every `.sql` file of the project a code file, also migrations, tests and BeforeDeploy/AfterDeploy scripts: do not use it."

- [x] Also reject an empty folder name in `settings.json` when the project is loaded? Maintainer, 2026-11-23: not in this task; the check is 0096-144.

## Docs to update
- `docs/llms-full.txt` §4
- `docs/project/configuration.md` (folder keys), if it mentions an empty folder

## Done when
- [x] Docs updated

## Notes
- `CodeFolder: ""` is supported, not a mistake: `ProjectFoldersTests.Root_Folder_Contains_All_Files` keeps code in the scripts root. So the docs do not say "do not use it", as the planned sentence did, but "only for a project that has nothing but code files".
- Verified on 2026-11-23 with a temporary unit test of `DbLiveProject` (`CodeFolder: ""`, the other folders at their defaults): migrations, BeforeDeploy and AfterDeploy scripts and `Tests/select.sql` are code files too; `Migrations/001.breaking.sql` and `Migrations/002.b.sql` are also breaking scripts of the Code folder; `Tests/x.test.sql` is found twice. The table and the check that would stop such a project: 0096-144.

## Result
- `docs/llms-full.txt` §4: the sentence is replaced. `CodeFolder: ""` makes the scripts root the Code folder, so the check of files outside the project folders never fails; use it only for a project of code files. The files of the other project folders then become code files as well, a migration breaking script also a breaking script of the Code folder, and a test of `Tests/` that matches `TestFilePatterns` is found twice.
- `docs/project/configuration.md` does not mention an empty folder: unchanged.
- The maintainer's answer to the question: the check is a task of its own, 0096-144.
