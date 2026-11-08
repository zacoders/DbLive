---
type: bug
priority: medium
created: 2026-11-09
---
# An exception of the transaction check hides the error of a code file (SQL Server)

## Problem
When a code file fails inside the deployment transaction (`TransactionWrapLevel: Deployment`), `CodeItemDeployer` asks the provider in its `catch` whether the transaction can still be used (`IsTransactionUsableAfterErrorAsync`, added by 0096-61). The call is not protected.

On SQL Server the check opens a new connection, which enlists in `Transaction.Current`. When the error aborted the `System.Transactions` transaction itself (the connection broke on an error of severity 20 or more, or the `TransactionScope` timed out after `DeploymentTimeout`), opening the connection throws `TransactionException` ("The operation is not valid for the state of the transaction"). The exception leaves the `catch`, so the file gets no `CodeItemDeployResult`: `Task.WhenAll` in `CodeDeployer.DeployGroupAsync` rethrows the bare `TransactionException`. The user gets no `CodeDeploymentAggregateException`, no path of the file and no SQL error, and none of them is logged.

From the code, not verified: whether opening the connection throws in this state is to be checked by a test.

PostgreSQL is not affected: its `IsTransactionUsableAfterErrorAsync` returns `false` without a query.

The branch without a transaction has the same shape: it writes the error row with `SaveCodeItemAsync` in the `catch`, and a lost connection makes the write throw and replace the error of the file. That code is older (it is in 1.5.0).

Found by the code review of 2026-11-09 (finding 4).

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:51-63` (the check), `:70` (the write of the error row)
- `src/DbLive.MSSQL/MsSqlDA.cs:13-19` (`IsTransactionUsableAfterErrorAsync`)

## Fix
Wrap the check in `try`/`catch`: an exception of the check means the transaction cannot be used, so `CanRetry = false`, and the result keeps the error of the file. Log the exception of the check as a warning.

- [ ] Also protect the write of the error row in the branch without a transaction, so the error of the file stays the result?

## Done when
- [ ] Reproduced on SQL Server, or ruled out: a code file that breaks its connection (`raiserror(..., 20, 1) with log`) in a deployment transaction
- [ ] Fixed, with a unit test: a data access whose `IsTransactionUsableAfterErrorAsync` throws, and the deployment fails with `CodeDeploymentAggregateException` that names the file
