---
type: feature
priority: medium
created: 2026-11-02
closed: 2026-11-02
breaking: true
---
# Fail the deployment on files outside the project folders

## Problem
A file outside the folders DbLive reads (`Migrations`, `Code`, `Tests`, `BeforeDeploy`, `AfterDeploy`, or the names set in `settings.json`) is ignored without a word. The AdventureWorks demo kept its post-deployment scripts in `PostDeployment` and they never ran (0096-16). A root file is no better: a `setting.json` typo leaves every setting at its default, and nobody notices.

## Decision
The maintainer chose an error, not a warning: the scripts folder belongs to DbLive. Scripts that should not be deployed go to a folder above it (for `SetProject`: outside `Scripts`, which the `.csproj` embeds; for `SetScriptsFolder`: point it at the scripts folder). No setting for ignored paths.

- Every file must be inside one of the five project folders, or be `settings.json` in the root.
- Hidden files and folders (the name starts with a dot) are skipped: Finder creates `.DS_Store` in every folder it opens, and with `SetScriptsFolder` that would fail the deployment on a developer's Mac.
- Files inside a project folder that match no pattern (`Code/notes.txt`, `Migrations/001.sql.bak`) stay ignored: out of scope.

## Plan
- Check in `DbLiveProject.GetFiles`, next to `ScriptPathsDifferOnlyByCaseException` and `BreakingScriptMatchesTestPatternException`: every read of the project runs it, so the deployment stops at its first read, before anything is deployed; `[SqlFact]` discovery fails the same way.
- Works with nested folder settings (`db/Code`) and with `CodeFolder: ""` (everything is code) without special cases.
- New `FilesOutsideProjectFoldersException`: the count, the first files by relative path, the project folders, and what to do.

## Where
- `src/DbLive/Project/DbLiveProject.cs` (`GetFiles`)
- `src/DbLive/Project/ProjectSettingsReader.cs` (`SettingsFileName`)
- `src/DbLive/Project/Exceptions/`
- Tests that relied on the silent skip: `ProjectFoldersTests.Nested_Folder_Settings_Work_With_Any_Separator`, `CodeItemsTests.GetCodeItems_No_Code_No_Groups`

## Docs to update
- `docs/llms-full.txt` §0 (new hard rule), §4 (discovery rules), §7 (the `PostDeployment` line), §17 (exception)
- `docs/project/structure.md` (folders, "Files with other extensions")
- `docs/llms.txt` (key facts)

## Done when
- [x] Fails on a file outside the project folders, with unit tests
- [x] Existing tests and demos green
- [x] Docs updated, release note (breaking change)

## Notes
- Came up while fixing 0096-16 (`tasks/done/0096-16-adventureworks-postdeployment-never-runs.md`).
- The message names the first 5 files, like `CodeNotDeployedException`: a whole folder of old scripts would otherwise make it unreadable. The relative paths are enough to see the folder.
- Three unit tests put files outside the project on purpose, to show that DbLive ignores them: the root `Migrations`, `Code`, ... next to nested folder settings (`ProjectFoldersTests`, `CodeBreakingItemsTests`) and `CodeX/a.sql` (`CodeItemsTests`). Those files are removed; `FilesOutsideProjectFoldersTests` now checks that they fail.
- Not skipped: Windows `Thumbs.db` and `desktop.ini`. Windows creates them only in folders with pictures or a custom icon, which a scripts folder has no reason for.
- All demo projects already pass: their roots hold only project folders and `settings.json` (AdventureWorks since 0096-16).

## Result
- `DbLiveProject.GetFiles` throws `FilesOutsideProjectFoldersException` (count, first 5 files, project folders) when a file is in none of the five project folders, is not the root `settings.json` and has no path part that starts with a dot. `SettingsAccessor.SettingsFileName` is internal now, so both use one name.
- `FilesOutsideProjectFoldersTests`: every read fails; root files and look-alike folders (`CodeX`, `Old/Code`) fail; `settings.json` in any case, hidden files and folders, and project folders in another case pass; the message names the first 5 files; nested folder settings.
- `dotnet test DbLive.sln`: 477 passed, 0 failed (unit tests, SQL Server and PostgreSQL integration tests, all demos).
- Docs: `docs/llms-full.txt` rule 16, §4, §7, §17; `docs/project/structure.md`; `docs/llms.txt`.

## Release note
Files outside the project folders stop the deployment. Every file of the scripts folder must be inside `Migrations`, `Code`, `Tests`, `BeforeDeploy` or `AfterDeploy` (or the folders named in `settings.json`), except `settings.json` in the root; hidden files such as `.DS_Store` are skipped. Before, such files were ignored without a word, so a `PostDeployment` folder never ran and a misspelled `setting.json` left every setting at its default. Now the deployment fails with `FilesOutsideProjectFoldersException` before anything is deployed. Move other scripts and documents out of the scripts folder, or rename the folder to a project folder.
