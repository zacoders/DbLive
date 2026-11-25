---
type: chore
priority: low
created: 2026-11-25
---
# Small code fixes from the review of 2026-11-25 (second)

## Problem
1. **PostgreSQL: a failed reload of the types fails a deployment that committed.** `src/DbLive/DbLive.cs:63` calls `_da.OnDeployedAsync()` inside the `try` after `_deployer.DeployAsync(...)`; on PostgreSQL it opens a new connection and runs `ReloadTypesAsync()` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:114-119`). An error there (a broken network, a restarted server) fails `DeployAsync` with a bare `NpgsqlException`, without DbLive's context, after the migrations and the code are committed; the next deployment deploys nothing and succeeds. The release of the deployment lock in the same method only logs its error, so as not to "fail a deployment that did its work" (`DbLive.cs:79`). §11.1 (line 781): "After a deployment that succeeded DbLive reloads the types for its connection string". Read in the code; the failure is rare.
2. **SQL Server, `Deployment`: a code file that fails and leaves a transaction open is reported as an error that ended the transaction.** For `Code/a.sql` `begin tran; exec dbo.Missing;` (a statement-level error, `XACT_ABORT` off) the error path of the guard (`src/DbLive.MSSQL/MsSqlDA.cs:431-438`) finds `@@trancount` above its start and rolls back the whole deployment transaction; `CodeItemDeployer.cs:77-83` then finds `IsTransactionUsableAsync` false and writes `The error ended the deployment transaction, so the file is not retried.` The error left the transaction usable; DbLive rolled it back for the transaction left open. The outcome is right (not retried, nothing committed), the reason is not. Read in the code.
3. **The XML docs of `TestRunResult` miss the test that left a transaction open.** `src/DbLive/Deployers/Testing/TestRunResult.cs:19-21, 28-31`: `ErrorMessage` lists an assertion, an error and a `COMMIT` or `ROLLBACK` that ended the transaction; `Exception` is null only when "an assertion did not hold, or the transaction ended". A SQL Server test that leaves a transaction open fails without `Exception` too, with `TransactionLeftOpenMessage` (`UnitTestItemRunner.cs:101-105`). §8.2 (line 467) is right.

## Fix
1. Catch the error of `OnDeployedAsync`, log a warning that the types of this process may be stale (with the fix: `ReloadTypesAsync()` of the user's own connection), and say so in §11.1.
   - [ ] Or keep the error: the SQL tests after the deployment in the same process would fail on the types anyway?
2. Let the guard say that the transaction was rolled back for a transaction left open, or make the message of `CodeItemDeployer` name both causes.
3. Add the case to the XML docs.

## Docs to update
- `docs/llms-full.txt` §11.1 (1)

## Done when
- [ ] 1–3 fixed, with tests where behavior changes

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0).
