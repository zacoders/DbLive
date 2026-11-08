---
type: bug
priority: low
created: 2026-11-08
---
# A code file is retried after its error rolled back the SQL Server transaction

## Problem
In the deployment transaction (`TransactionWrapLevel: Deployment`) a failed code file is retried when `CanRetry = !_da.ErrorAbortsTransaction`, which is always true on SQL Server (0096-51). Some errors roll back the transaction on SQL Server too: any error in a file that sets `set xact_abort on;`, a deadlock (1205). Then each retry would fail on the rolled-back transaction ("The transaction operation cannot be performed..." or "Distributed transaction completed..."), and `CodeDeploymentAggregateException` would carry the error of the last attempt, not "Invalid object name ...". The first error is only in the `Debug` log of `CodeDeployer`.

Not verified. 0096-51 tested `XACT_ABORT ON` only in a breaking script of the Code folder, which is not retried, and that test passes. The retry of a code file is a different path.

Found by the code review of 2026-11-08 (finding 4).

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:50-58` (`CanRetry`)
- `src/DbLive/Deployers/Code/CodeDeployer.cs:184-204` (the retry, the `Debug` log of an attempt)
- `src/DbLive/Adapter/IDbLiveDA.cs` (`ErrorAbortsTransaction`), `src/DbLive.MSSQL/MsSqlDA.cs`

## Fix
- [ ] Reproduce first: an integration test on SQL Server, `TransactionWrapLevel: Deployment`, a code file `set xact_abort on; create view ... from dbo.Missing`. What does the deployment fail with?
- [ ] If reproduced, which fix: decide `CanRetry` from the error (the transaction is no longer active: `Transaction.Current.TransactionInformation.Status`, or the error number), or keep the first error of a file and report it with the last one?

## Docs to update
- `docs/llms-full.txt` §6 (order and retries), §17 (`CodeDeploymentAggregateException`)
- `docs/deployment/code.md` ("Order, parallelism and retries")

## Done when
- [ ] Reproduced, or closed as not reproducible with the test kept
- [ ] Fixed, with the integration test on SQL Server
- [ ] Docs updated
