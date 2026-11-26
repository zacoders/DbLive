---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# `RunAllTestsAsync` names a failed test by its file name only

## Release note
`RunAllTestsAsync` logs each SQL test by its relative path (`PASSED: Tests/Orders/select.sql`) instead of its file name, so tests with the same name in different folders can be told apart. The message of `UnitTestsFailedException` names the failed tests, the first ten by relative path, so a failed `[SqlDeployFact]` shows them without the log. Nothing to change on upgrade.

## Problem
- `src/DbLive/Deployers/Testing/UnitTestsRunner.cs:39` logs `PASSED: {testName}` and `:45` logs `FAILED: {filePath}. Error Message: ...` with `test.Name`, which is `Path.GetFileName(relativePath)` (`src/DbLive/Project/DbLiveProject.cs:230`), not the path the placeholder promises.
- The message of `UnitTestsFailedException` is "Unit test run failed. Failed test count X of Y." (`UnitTestsFailedException.cs:11`) and lists no test. `FailedTests` has the paths, but `[SqlDeployFact]` shows only the message and the log.

Two failed tests with the same file name in different folders (`Tests/Orders/select.sql` and `Tests/Users/select.sql`, or `GetUser.test.sql` in two schema folders) cannot be told apart in the log.

## Fix
Log `test.FileData.RelativePath`. Add the sorted `FailedTests` to the message of `UnitTestsFailedException` (the first ten, then "and N more").

## Docs to update
- `docs/llms-full.txt` §17 (row `UnitTestsFailedException`), if the message changes

## Done when
- [x] Fixed, with a unit test of the message
- [x] Docs updated

## Result
- `UnitTestsRunner` logs `PASSED: {filePath}` and `FAILED: {filePath}. Error Message: {errorMessage}` with the relative path of the test file. The property of the `PASSED` line is `filePath` now, as in the `FAILED` line (was `testName`).
- `UnitTestsFailedException`: "Unit test run failed. Failed test count X of Y: 'Tests/a.sql', 'Tests/b.sql'.", the first ten of `FailedTests` (sorted by the runner), then " and N more". The format of the list follows `FilesOutsideProjectFoldersException`. Its XML doc says so.
- Unit tests (`UnitTestsRunnerTests`): the log lines and the message by path; three tests named `select.sql` in different folders; twelve failed tests, given in reverse, name the first ten sorted and "and 2 more". Integration tests on both providers (`CrossPlatformDeployTests.Failed_test_is_reported_by_the_real_path`) check the path in the message.
- Docs: `docs/llms-full.txt` §8.2 (the log lines and the message) and §17 (row `UnitTestsFailedException`); `docs/unit-testing/index.md`, "Running all tests without xUnit".
