---
type: chore
priority: low
created: 2026-11-25
closed: 2026-11-26
---
# Small code fixes from the review of 2026-11-25 (second)

## Release note
None. It refines behavior that is new in this release: on SQL Server a code file that fails inside a transaction it began, in the deployment transaction, names that reason in its message; on PostgreSQL the log says that the deployment is committed when the reload of the types after it fails.

## Problem
1. **PostgreSQL: a failed reload of the types fails a deployment that committed.** `src/DbLive/DbLive.cs:63` calls `_da.OnDeployedAsync()` inside the `try` after `_deployer.DeployAsync(...)`; on PostgreSQL it opens a new connection and runs `ReloadTypesAsync()` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:114-119`). An error there (a broken network, a restarted server) fails `DeployAsync` with a bare `NpgsqlException`, without DbLive's context, after the migrations and the code are committed; the next deployment deploys nothing and succeeds. The release of the deployment lock in the same method only logs its error, so as not to "fail a deployment that did its work" (`DbLive.cs:79`). §11.1 (line 781): "After a deployment that succeeded DbLive reloads the types for its connection string". Read in the code; the failure is rare.
2. **SQL Server, `Deployment`: a code file that fails and leaves a transaction open is reported as an error that ended the transaction.** For `Code/a.sql` `begin tran; exec dbo.Missing;` (a statement-level error, `XACT_ABORT` off) the error path of the guard (`src/DbLive.MSSQL/MsSqlDA.cs:431-438`) finds `@@trancount` above its start and rolls back the whole deployment transaction; `CodeItemDeployer.cs:77-83` then finds `IsTransactionUsableAsync` false and writes `The error ended the deployment transaction, so the file is not retried.` The error left the transaction usable; DbLive rolled it back for the transaction left open. The outcome is right (not retried, nothing committed), the reason is not. Read in the code.
3. **The XML docs of `TestRunResult` miss the test that left a transaction open.** `src/DbLive/Deployers/Testing/TestRunResult.cs:19-21, 28-31`: `ErrorMessage` lists an assertion, an error and a `COMMIT` or `ROLLBACK` that ended the transaction; `Exception` is null only when "an assertion did not hold, or the transaction ended". A SQL Server test that leaves a transaction open fails without `Exception` too, with `TransactionLeftOpenMessage` (`UnitTestItemRunner.cs:101-105`). §8.2 (line 467) is right.

## Fix
1. Catch the error of `OnDeployedAsync`, log a warning that the types of this process may be stale (with the fix: `ReloadTypesAsync()` of the user's own connection), and say so in §11.1.
   - [x] Or keep the error: the SQL tests after the deployment in the same process would fail on the types anyway? Yes, keep it, with DbLive's context in the log (maintainer, 2026-11-26).
2. Let the guard say that the transaction was rolled back for a transaction left open, or make the message of `CodeItemDeployer` name both causes.
3. Add the case to the XML docs.

## Docs to update
- [x] `docs/llms-full.txt` §11.1 (1), §6 and §10 and the `CodeDeploymentAggregateException` row of §17 (2)
- [x] `docs/deployment/index.md` (PostgreSQL types, 1)
- [x] `docs/deployment/code.md` (retries, 2)

## Done when
- [x] 1–3 fixed, with tests where behavior changes

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0).
- 1: a warning is worse than the error. The process of the deployment keeps the types it loaded before the migrations: the SQL tests after it (DbLive.xunit) and an application that deploys at its startup would fail later on an enum or domain of the deployment with `InvalidCastException`, far from the cause, and a warning in the log is easy to miss. The error fails at once, and running the deployment again is safe: it deploys nothing and calls `OnDeployedAsync` again, which reloads the types. Only a deployer in a process of its own (a CI step) is better off with a warning, and its rerun costs nothing. Decision of the maintainer (2026-11-26): keep the error and log DbLive's context with it.
- 2: the message of `CodeItemDeployer` cannot name both causes in a neutral way: on PostgreSQL the second cause does not exist (`BEGIN` in a transaction is a warning, and a failed file is rolled back to its savepoint). A wrapping exception (`DbLiveTransactionLeftOpenException` with the error inside) was not taken: a SQL test would then report `TransactionLeftOpenMessage` without the error of the test, and the type of the error would change for every caller. The guard marks the error it rethrows instead (`DbLiveSqlException.RolledBackWithTransactionLeftOpen`, internal); every error of a script that the guard sees is a `DbLiveSqlException`, also a timeout (`ExecutionTimeoutException`). Outside the deployment transaction the mark changes nothing: the transaction of a code file of its own is rolled back as a whole, and the file is retried as before.
- 2, verified on SQL Server: `begin tran; exec dbo.Missing;` in the deployment transaction fails with `... The file failed inside a transaction that it began, and DbLive rolled back the deployment transaction with it, so it is not retried.`, and nothing is committed.

## Result
1. `DbLive.DeployAsync` still throws the error of `OnDeployedAsync`, and logs it first as an error: the deployment is committed, the types of the database were not reloaded for the connections of the process, and running the deployment again reloads them. Test: `DbLiveTests.Error_after_the_deployment_is_thrown_and_logged_as_committed`.
2. The error path of the SQL Server guard (`MsSqlDA.ExecuteGuardedAsync`) sets `DbLiveSqlException.RolledBackWithTransactionLeftOpen` when it rolled back a transaction that the script left open; `CodeItemDeployer` then writes `The file failed inside a transaction that it began, and DbLive rolled back the deployment transaction with it, so it is not retried.` without asking `IsTransactionUsableAsync`. Tests: two in `CodeItemDeployerTests` (in the deployment transaction and without it), and `CodeRetryInDeploymentTransactionTests.Code_file_that_fails_inside_its_own_transaction_fails_the_deployment_at_once_with_that_reason` on SQL Server.
3. The XML docs of `TestRunResult.ErrorMessage` and `Exception` name the test that left a transaction open.
