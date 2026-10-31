---
type: bug
priority: low
created: 2026-10-26
closed: 2026-10-31
---
# Nested paths in folder settings and `CodeSubFoldersDeploymentOrder` work only with the OS separator

## Problem
Relative paths from the settings were combined as is (`Path.Combine` does not normalize separators), while enumerated files use the OS separator:
- `CodeSubFoldersDeploymentOrder`: `"dbo\\functions"` didn't match on Linux (`\` is a plain file name character there) and `"dbo/functions"` didn't match on Windows. The group silently ended up empty, and its files went to the last "everything else" group. Deployment usually still succeeded thanks to retries, but the configured order was ignored. `DemoMSSQL/Scripts/settings.json` uses `"dbo\\functions"`.
- Folder settings (`MigrationsFolder`, `CodeFolder`, `TestsFolder`, `BeforeDeployFolder`, `AfterDeployFolder`): a nested value with `\` pointed to a non-existent folder on Linux, and the folder was silently skipped (a missing folder is valid). On Windows both separators worked: `Directory.EnumerateFiles` keeps the given prefix (`...\db/Code\dbo\f.sql`), and `Path.GetRelativePath` normalizes it.

Verified before the fix: a Windows check of the same logic matched `dbo\functions` and not `dbo/functions`; a `CodeFolder` of `db/Code` worked on Windows.

## Where
`src/DbLive/Project/DbLiveProject.cs` (`GetCodeGroupsAsync`, `GetMigrationsAsync`, `GetTestsAsync`, `GetFolderItemsAsync`), `src/DbLive/Common/StringExtensions.cs`.

## Fix
`StringExtensions.CombineWithRelative`: normalizes `\` and `/` to `Path.DirectorySeparatorChar` and trims leading and trailing separators before `Path.Combine`. Used for every relative path from the settings: the five folder settings and `CodeSubFoldersDeploymentOrder` entries. Log a warning when a `CodeSubFoldersDeploymentOrder` group matches no files.

## Docs to update
- `docs/llms-full.txt` §4 (intro), §6 (rewrite the limitation bullet), §9.2 (`MigrationsFolder` row, note after the table), §19 (remove the limitation)
- `docs/project/configuration.md` (paragraph after the Folders table)
- `docs/deployment/code.md` ("Order, parallelism and retries")

## Done when
- [x] Separators normalized for `CodeSubFoldersDeploymentOrder` and all folder settings, with unit tests for both separators
- [x] Warning for an empty group
- [x] Docs updated

## Notes
- Trailing separator (`"dbo/functions/"`) would give a double separator in the prefix; leading separator (`"/dbo/functions"`) makes `Path.Combine` drop the base folder. Both are trimmed.
- The empty group is still returned (as before), only the warning is new.
- No warning for a missing folder from the folder settings: a missing folder is valid (for example a project without `Migrations`).
- The project path itself (`IProjectPath.Path`) is not normalized: it is an absolute path from the caller, not a setting.

## Result
- `StringExtensions.CombineWithRelative` replaces `CombineWith` / `Path.Combine` for the folder settings and the code subfolders in `DbLiveProject`. `DbLiveProject` takes `ILogger`; warning `Code subfolder '{SubFolder}' from CodeSubFoldersDeploymentOrder matches no code files.`
- Tests:
  - `StringExtensionsTests.CombineWithRelative_*`: `db\Code`, `db/Code`, trailing, leading and mixed separators, a top-level folder.
  - `ProjectFoldersTests.Nested_Folder_Settings_Work_With_Any_Separator` (`\` and `/`): all five folder settings set to `db<sep><name>`, checks the folder paths `DbLiveProject` looks for.
  - `CodeItemsTests.GetCodeItems_With_Nested_CodeSubFoldersDeploymentOrder` (`dbo\functions`, `dbo/functions`, `dbo/functions/`, `\dbo\functions\`) and `GetCodeItems_Warns_When_CodeSubFolder_Matches_No_Files`.
  - Without the fix on Windows the `/` variants of the `DbLiveProject` tests and the warning test fail (the `\` variants pass, the folder tests check the exact path).
  - All pass on Windows (`DbLive.Tests` 268) and on Linux (the new tests called directly from a console app in the `dotnet/sdk:8.0` container: VSTest 17.11 from SDK 8 does not discover tests built with Test.Sdk 18).

Release note (copy into the release):

> **Fixed: nested paths in settings work on any OS.** Folder settings (`MigrationsFolder`, `CodeFolder`, `TestsFolder`, `BeforeDeployFolder`, `AfterDeployFolder`) and `CodeSubFoldersDeploymentOrder` entries may use `/` or `\`: `"dbo/functions"` and `"dbo\\functions"` are the same on Windows and Linux. A `CodeSubFoldersDeploymentOrder` entry that matches no code files is logged as a warning.
