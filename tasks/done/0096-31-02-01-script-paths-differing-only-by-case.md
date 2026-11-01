---
type: bug
priority: low
created: 2026-11-01
closed: 2026-11-01
---
# Two scripts whose paths differ only by case share one row in `dblive`

## Problem
Since 0096-31-02 `relative_path` is stored in lower case. On Linux a project may have two files whose paths differ only by case (`Code/dbo/GetUser.sql` and `Code/dbo/getuser.sql`; `DbLiveProject.GetFolderFiles` sorts them with a second `StringComparer.Ordinal` key for this case). Both map to one row:
- Code: each file finds the hash of the other one in `dblive.code`, so both are redeployed on every deployment. With parallel code deploy they may also save the same row at the same time (the SQL Server `merge` has no `holdlock`, so two inserts of a new row can fail with a primary key violation).
- Tests and BeforeDeploy/AfterDeploy: the two files overwrite each other's last result.

Such a checkout cannot exist on Windows or macOS (case-insensitive file systems), so the project is not portable anyway.

## Fix
- [x] Fail early when two script paths differ only by case: an exception that names both paths, thrown while reading the project (`DbLiveProject` or the script source), for code, tests and folder items. Migrations are identified by version and already fail on a duplicate version.
- [x] Unit test with `InMemoryScriptSource`.

## Docs to update
- `docs/llms-full.txt` §4 (discovery rules) and §17 (exceptions)

## Done when
- [x] Two scripts whose paths differ only by case stop the deployment with a clear message, with a unit test
- [x] Docs updated

## Notes
2026-11-01, while implementing:
- The check is in `DbLiveProject` over all files of the project (`GetFiles()`), not only over the files of the folder being read. Every read runs it, so a deployment stops at its first read of the project: `MigrationChecksumValidator` / `MigrationsSaver` read migrations before BeforeDeploy, migrations and code are deployed. A check per folder would find code duplicates only after migrations were applied, and duplicates in `Tests` only after the deployment was committed.
- Cost: two ignored files (`notes.txt` and `Notes.txt`) also fail. Accepted: such a checkout is broken on Windows and macOS anyway, and the rule "no two paths that differ only by case" is simple to state.
- Checking all files also covers what the folder reads exclude: `setup.test.sql` (two setup files of one folder, one was picked silently by `ReadSetupFileAsync`), and two `settings.json` files at the root (`SettingsAccessor` picks one, then the first read of the project fails).
- Migrations: `001.migration.sql` and `001.Migration.sql` failed before with `DuplicateMigrationItemException` (the type is parsed ignoring case); now the new exception, which names both paths, comes first.
- Not in the script sources: `InMemoryScriptSource` would need the same check, and the unit test would then test the test helper.
- The second sort key `StringComparer.Ordinal` in `GetFolderFiles` is removed: paths that differ only by case no longer reach it.

## Result
- New `ScriptPathsDifferOnlyByCaseException` (`src/DbLive/Project/Exceptions/`), message `Files '<path>' and '<path>' differ only by case. ...`, property `RelativePaths`.
- `DbLiveProject`: all reads (`GetCodeGroupsAsync`, `GetMigrationsAsync`, `GetTestsAsync`, `GetFolderItemsAsync`) get the files through `GetFiles()`, which throws the exception for the first pair of paths that differ only by case. `[SqlFact]` discovery and `IDbLiveTester` fail the same way, as they read tests through `DbLiveProject`.
- Tests: `src/DbLive.Tests/Project/PathsDifferingOnlyByCaseTests.cs` (code, test folders, setup files, BeforeDeploy/AfterDeploy, migrations, duplicates in `Code` found when migrations are read). All 407 tests pass, including the Docker integration tests and the demo tests.
- Docs: `docs/llms-full.txt` §3.4, §4, §17; `docs/project/structure.md`, `docs/project/index.md`.
