---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The error of a failed `setup.test.sql` does not say that the setup failed

## Release note
A SQL test whose `setup.test.sql` fails now says so in `ErrorMessage`, with the path of the setup file: `Setup file error (Tests/Orders/setup.test.sql): <error>`. Before, only the output of the test said that the setup failed, so the failure of `[SqlFact]` (`Assert.True(result.IsSuccess, result.ErrorMessage)`) and the log of `RunAllTestsAsync` showed an error of the setup file, a timeout too, as if the test file had failed. Nothing to change on upgrade.

## Problem
`docs/llms-full.txt` §8.2 (line 461): "If the setup file fails, the test fails with `ERROR: Setup file error. ...`". The decision in `tasks/done/0096-86-...` says that "the result of the test says when it was setup.test.sql".

The marker is only in `TestRunResult.Output`. `ErrorMessage` gets the raw error (`src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:96-110`):
- `[SqlFact]` fails with `result.ErrorMessage`, so its failure message does not mention the setup file; only the test output does.
- `RunAllTestsAsync` logs only `ErrorMessage` (`UnitTestsRunner.cs:45`), so the log and `[SqlDeployFact]` give no hint. A setup file that runs out of time reports "Execution Timeout Expired. The script ran longer than its timeout of 00:01:00." against the test file.

## Where
- `src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:96-110`

## Fix
`ErrorMessage = $"Setup file error ({setup relative path}): {ex.Message}"`, and the same text in `Output`.

## Docs to update
- `docs/llms-full.txt` §8.2 (the message text)
- `docs/unit-testing/index.md` (section on `setup.test.sql`), if it quotes the message

## Done when
- [x] Fixed, with a unit test of the runner: a failing setup gives an `ErrorMessage` that names the setup file
- [x] Docs updated

## Notes
- 2026-11-23: 0096-141 dropped `dblive.unit_test_run`: `RunAllTestsAsync` only logs `ErrorMessage` now (`UnitTestsRunner.cs:45`). The marker is needed in the log and in `ErrorMessage`.

## Result
- `UnitTestItemRunner`: when `setup.test.sql` fails, `ErrorMessage` is `Setup file error (<relative path of the setup file>): <error>`, and `Output` is `ERROR: ` and the same text. The error of the test file is unchanged; its `Output` now has a space after `ERROR:`, as the setup error had. The error of a transaction that the setup file ended stays the message of `TransactionEndedMessage`, which names `setup.test.sql` itself.
- Unit tests (`UnitTestItemRunnerTests`): a failed setup names the setup file in `ErrorMessage` and `Output` and does not run the test file; a test file that fails after its setup keeps its own error.
- Integration tests on SQL Server and PostgreSQL (`CommandTimeoutTests.Command_timeout_of_a_setup_file_names_the_setup_file`): a setup file that runs out of `UnitTestItemTimeout` fails the test with `Setup file error (Tests/Orders/setup.test.sql): Execution Timeout Expired. ...`.
- Docs: `docs/llms-full.txt` §8.2 (the message, also for the timeout of the setup file); `docs/unit-testing/index.md`, section "Shared setup with `setup.test.sql`".
