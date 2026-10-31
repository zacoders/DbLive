---
type: bug
priority: low
created: 2026-10-26
closed: 2026-10-31
---
# Test setup file: `init.sql` in `Code` is deployed as code, rename to `setup.test.sql`

## Problem
Code deployment excluded only files matching `TestFilePatterns`. An `init.sql` next to tests in `Code` was attached to the tests of its folder and also deployed as a code object: it ran (without rollback) whenever its content changed and left test data in the database (verified in code: `GetCodeGroupsAsync` did not exclude it, `GetFolderTestsAsync` loaded it for `Code` folders too). The docs worked around it with "keep `init.sql` in `Tests` only".

The name `init.sql` also says nothing about tests when it sits next to code in `Code`.

## Where
`src/DbLive/Project/DbLiveProject.cs` (`GetCodeGroupsAsync`, `GetFolderTestsAsync`), `TestItem`, `UnitTestItemRunner`, `DbLiveVisualStudioTester`.

## Fix
Rename the test setup file to `setup.test.sql` (no library users yet, so no compatibility for the old name) and support it in `Tests` and `Code`:
- "setup" is the usual term (tSQLt `SetUp` has the same semantics: before each test, inside its transaction);
- the `.test.sql` suffix marks it as a test file next to code and matches the default `TestFilePatterns`;
- code deployment excludes it explicitly, so custom `TestFilePatterns` do not matter.

## Docs to update
- `docs/llms-full.txt` §0 rule 7, §4, §6, §8.1, §8.2, §8.3, §8.4, §17
- `docs/project/structure.md` (tree, folders table, "Test files")
- `docs/unit-testing/index.md` ("Where tests live", `GO` warning, `rows` note, "Shared setup with `setup.test.sql`")
- `README.md`, `docs/index.md`, `docs/llms.txt`, `docs/examples/index.md`, `docs/unit-testing/stored-proc-and-unit-test.md`

## Done when
- [x] `setup.test.sql` excluded from code, with a unit test
- [x] `init.sql` renamed in code, tests and demos
- [x] Docs updated

## Notes
- Considered: excluding `init.sql` only (the name stays unclear in `Code`), forbidding the file in `Code` (loses shared setup for tests next to code), a setting for the file name (not needed).
- An old `init.sql` is now a plain test in `Tests` and a code object in `Code`. No exception for the old name, since there are no users; `llms-full.txt` §4 mentions the old name for agents that know it.
- Case: on Linux `Setup.test.sql` is neither excluded nor loaded as setup (`FileExists` and the enumeration patterns are case-sensitive there). Consistent, so not handled.

## Result
- `DbLiveProject.TestSetupFileName = "setup.test.sql"`, used by test discovery and excluded in `GetCodeGroupsAsync` together with `TestFilePatterns`.
- `TestItem.InitFileData` → `SetupFileData`; messages `ERROR: Setup file error. ...` (test run) and `Setup file: <path>` (Visual Studio output).
- Tests: `CodeItemsTests.GetCodeItems_Excludes_TestSetupFile` (custom `TestFilePatterns` that do not match the setup file; fails without the fix); `SqlTestingTests.GetFolderTests_excludes_setup_file_from_test_files` now checks the exclude list.
- Demos: `DemoMSSQL` `Tests/Common`, `Tests/Orders` and `AdventureWorks` `Tests/Functions` use `setup.test.sql`. Testcontainers runs pass (DemoMSSQL 17, AdventureWorks 10), including the `Tests/Orders` tests that depend on the setup data. `LocalDeployTests` fails on the local database with a checksum mismatch of `007.migration.sql` (changed in `dc78014` after it was applied there), unrelated to this task.

Release note (copy into the release):

> **Breaking: the test setup file is now `setup.test.sql` (was `init.sql`).** It runs before each test in its folder, in `Tests` and in `Code`, and is never deployed as code. Rename your `init.sql` files: an `init.sql` is now a plain test in `Tests` and a code object in `Code`.
