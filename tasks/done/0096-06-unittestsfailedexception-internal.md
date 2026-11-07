---
type: bug
priority: low
created: 2026-10-26
closed: 2026-10-31
---
# `UnitTestsFailedException` is internal

## Release note
`UnitTestsFailedException`, thrown when SQL tests fail, is now public, with `FailedCount`, `TotalCount` and `FailedTests` (relative paths of the failed test files), so a caller can catch it by type. `InvalidMigrationItemTypeException` is now public too.

## Problem
Test failures can't be caught by type, only with `catch (Exception)`. A deployer can't tell "deploy succeeded, tests failed" from "deploy failed".

## Where
`src/DbLive/Deployers/Testing/UnitTestsFailedException.cs:6`.

## Fix
Make the class `public`. Optionally add `FailedCount`, `TotalCount` and the list of failed files.

## Docs to update
- `docs/llms-full.txt` §8.2, §17 (remove "internal type")
- `docs/unit-testing/index.md` (section "Isolation and parallelism": name the type)

## Done when
- [x] Class is public
- [x] Docs updated

## Notes
- `InvalidMigrationItemTypeException` had the same problem: `internal`, but thrown to the user while reading the project and listed in `docs/llms-full.txt` §4. Fixed here too.
- `InternalException` stays internal: it marks a bug inside DbLive, not something a caller handles.

## Result
- `UnitTestsFailedException` is public, in the style of the other exceptions (primary constructor, `[ExcludeFromCodeCoverage]`). Properties: `FailedCount`, `TotalCount`, `FailedTests` (relative paths of the failed test files, the same key as `dblive.unit_test_run`, sorted, ordinal). The message text is unchanged.
- `UnitTestsRunner` collects failed paths in a `ConcurrentBag` (tests run in parallel) and passes them to the exception.
- `InvalidMigrationItemTypeException` is public.
- Tests: `UnitTestsRunnerTests.RunTest_OneTestFailed_RunsAllTests_AndSavesAllResults` checks the counts, the failed path and the message. `DbLive.Tests` green (253).
- Docs: `docs/llms-full.txt` §8.2 (properties, `catch` example), §17; `docs/unit-testing/index.md` ("Isolation and parallelism").
