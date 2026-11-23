---
type: bug
priority: low
created: 2026-11-23
---
# The error of a failed `setup.test.sql` does not say that the setup failed

## Problem
`docs/llms-full.txt` §8.2 (line 454): "If the setup file fails, the test fails with `ERROR: Setup file error. ...`". The decision in `tasks/done/0096-86-...` says that "the result of the test says when it was setup.test.sql".

The marker is only in `TestRunResult.Output`. `ErrorMessage` gets the raw error (`src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:96-102`):
- `[SqlFact]` fails with `result.ErrorMessage`, so its failure message does not mention the setup file; only the test output does.
- `RunAllTestsAsync` logs and saves only `ErrorMessage` (`UnitTestsRunner.cs:46`, `:57`), so the log, `[SqlDeployFact]` and `dblive.unit_test_run.error` give no hint. A setup file that runs out of time reports "Execution Timeout Expired. The script ran longer than its timeout of 00:01:00." against the test file.

## Where
- `src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:96-102`

## Fix
`ErrorMessage = $"Setup file error ({setup relative path}): {ex.Message}"`, and the same text in `Output`.

## Docs to update
- `docs/llms-full.txt` §8.2 (the message text)
- `docs/unit-testing/index.md` (section on `setup.test.sql`), if it quotes the message

## Done when
- [ ] Fixed, with a unit test of the runner: a failing setup gives an `ErrorMessage` that names the setup file
- [ ] Docs updated
