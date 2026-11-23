---
type: bug
priority: low
created: 2026-11-23
---
# `RunAllTestsAsync` names a failed test by its file name only

## Problem
- `src/DbLive/Deployers/Testing/UnitTestsRunner.cs:39` logs `PASSED: {testName}` and `:45` logs `FAILED: {filePath}. Error Message: ...` with `test.Name`, which is `Path.GetFileName(relativePath)` (`src/DbLive/Project/DbLiveProject.cs:230`), not the path the placeholder promises.
- The message of `UnitTestsFailedException` is "Unit test run failed. Failed test count X of Y." (`UnitTestsFailedException.cs:11`) and lists no test. `FailedTests` has the paths, but `[SqlDeployFact]` shows only the message and the log.

Two failed tests with the same file name in different folders (`Tests/Orders/select.sql` and `Tests/Users/select.sql`, or `GetUser.test.sql` in two schema folders) cannot be told apart in the log.

## Fix
Log `test.FileData.RelativePath`. Add the sorted `FailedTests` to the message of `UnitTestsFailedException` (the first ten, then "and N more").

## Docs to update
- `docs/llms-full.txt` §17 (row `UnitTestsFailedException`), if the message changes

## Done when
- [ ] Fixed, with a unit test of the message
- [ ] Docs updated
