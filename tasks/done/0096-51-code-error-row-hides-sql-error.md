---
type: bug
priority: medium
created: 2026-11-07
closed: 2026-11-07
---
# The error row of a failed code file hides its SQL error

## Release note
**A failed code file reports its own error in a deployment transaction on PostgreSQL.** With `TransactionWrapLevel: Deployment`, a code file or a breaking script of the `Code` folder that failed made the deployment fail with `25P02: current transaction is aborted`, without the file name and the SQL error. Now it fails with `CodeDeploymentException` (inside `CodeDeploymentAggregateException` for a code file), which names the file and has the SQL error as its inner exception. The message of `CodeDeploymentAggregateException` is now `Code deployment failed.`, without the number of attempts.

## Problem
After a code file (`CodeItemDeployer`) or a code breaking script (`CodeBreakingDeployer`) fails, the deployer saves an `error` row in `dblive.code` and then reports the error. On PostgreSQL an error aborts the transaction, so in a deployment transaction (`TransactionWrapLevel: Deployment`) the save fails with `25P02: current transaction is aborted, commands ignored until end of transaction block`, and that exception leaves the deployment instead of `CodeDeploymentException` with the file name and the SQL error (verified 2026-11-07 by the new tests, before the fix: `Npgsql.PostgresException : 25P02` from `SaveCodeItemAsync` in both deployers).

For a code file there is a second step: `CodeDeployer` retries a failed file, and in the aborted transaction every retry fails with `25P02` too, so after the last attempt the error would still be `25P02`.

Found by the code review of 2026-11-07 (finding 3).

## Where
- `src/DbLive/Deployers/Code/CodeBreakingDeployer.cs`, `src/DbLive/Deployers/Code/CodeItemDeployer.cs`: the save of the error row in `catch`.
- `src/DbLive/Deployers/Code/CodeDeployer.cs`: the retry of a failed file.
- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: whether an error aborts the transaction.

## Fix
- In a transaction (`Transaction.Current`, that is `TransactionWrapLevel: Deployment`) both deployers write no `error` row: nobody would see it, a failed deployment rolls it back and a successful retry of a code file overwrites it with `applied`. Outside a transaction the row is written as before, and an error of that write fails the deployment.
- `IDbLiveDA.ErrorAbortsTransaction` (PostgreSQL true, SQL Server false). `CodeItemDeployResult.CanRetry` is false for a code file that failed in a transaction the error aborted; `CodeDeployer` does not retry such a file and fails at once with its error. On SQL Server a code file in the deployment transaction is retried as before.

## Docs to update
- `docs/llms-full.txt` §6 (order and retries), §6.1 (the row of a breaking script), §17 (`CodeDeploymentAggregateException`)
- `docs/deployment/code.md` ("Order, parallelism and retries", "Things to know")

## Done when
- [x] Fixed, with unit tests and integration tests on PostgreSQL and SQL Server
- [x] Docs updated

## Notes
- 2026-11-07, maintainer: the first fix (`CodeErrorRow.TrySaveAsync`) is rejected. It caught any error of the write, called it an aborted transaction in a warning and fell back to the script error; any failed write also turned off the retries. A broken `dblive.code` or a lost connection was hidden that way. Decision: write no `error` row in a transaction, and take the retry from `IDbLiveDA.ErrorAbortsTransaction`, not from the outcome of the write.
- Outside a transaction an error of the write is not caught, as before this task: the deployment fails with it. The script error of a code file is then not in the log: `CodeItemDeployer` does not log it before the write, because a code file that fails on dependency order and succeeds on a retry would log it on every attempt. `CodeBreakingDeployer` logs it before the write.
- The review also named SQL Server with `XACT_ABORT ON` in the script. Not reproduced (2026-11-07): `Failed_code_breaking_script_fails_with_its_own_error` with `set xact_abort on; drop proc dbo.GetUser_v1;` and `TransactionWrapLevel: Deployment` passes without the fix too. The test stays as a check of the contract on SQL Server; the comments name only PostgreSQL.
- On PostgreSQL with `TransactionWrapLevel: Deployment` the retries of code files never worked: the first error aborts the transaction. The docs now say so and point to `CodeSubFoldersDeploymentOrder` for the order of dependencies.

## Result
- `CodeBreakingDeployer` and `CodeItemDeployer` write the `error` row only outside a transaction; `IDbLiveDA.ErrorAbortsTransaction` (new); `CodeItemDeployResult.CanRetry`; `CodeDeployer` stops at once on a file that cannot be retried, and the aggregate message no longer names a number of attempts.
- Tests: unit (`CodeBreakingDeployerTests`, `CodeItemDeployerTests`: no `error` row in a transaction, the retry by `ErrorAbortsTransaction`, an error of the write is not hidden; `CodeIDeployerTests.DeployCode_Does_Not_Retry_An_Item_That_Cannot_Be_Retried`); PostgreSQL `CodeBreakingTests.Failed_code_breaking_script_fails_with_its_own_error` (`Migration` and `Deployment`) and `Failed_code_file_in_a_deployment_transaction_fails_with_its_own_error`, both failed before the fix; SQL Server `CodeBreakingTests.Failed_code_breaking_script_fails_with_its_own_error` and `Failed_code_file_in_a_deployment_transaction_is_retried` (verified: the view runs twice, the second time after the function).
