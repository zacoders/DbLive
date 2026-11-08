---
type: bug
priority: low
created: 2026-11-08
closed: 2026-11-08
---
# A code file is retried after its error rolled back the SQL Server transaction

## Release note
**A failed code file no longer leaves a partial deployment on SQL Server.** With `TransactionWrapLevel: Deployment`, a code file whose error rolled back the deployment transaction (any run-time error with `XACT_ABORT ON`, a missing dependency included; a failed conversion; a deadlock) was retried, and the retry and the code files after it ran outside the transaction: they were committed, while the migrations and files before the error were rolled back. The deployment failed with `TransactionAbortedException: The transaction has aborted.` or with the error of the file. Such a file is no longer retried: the deployment fails at once with its error and nothing is committed. The message of such a file, also on PostgreSQL, ends with `The error ended the deployment transaction, so the file is not retried.` Nothing to change on upgrade: these deployments failed before too. If code files set `xact_abort on` and rely on retries for their dependency order, leave `set xact_abort on` out of them, or order the dependencies with `CodeSubFoldersDeploymentOrder`.

## Problem
In the deployment transaction (`TransactionWrapLevel: Deployment`) a failed code file is retried when `CanRetry = !_da.ErrorAbortsTransaction`, which is always true on SQL Server (0096-51). Some errors roll back the transaction on SQL Server too: any run-time error in a file that sets `set xact_abort on;`, including a missing dependency (`Invalid object name`), a batch-aborting error such as a failed conversion, a deadlock (1205).

Verified (2026-11-08, see Notes): after such an error SqlClient does not fail the next commands. They run in autocommit mode and are committed at once. So the retries and the code files after the failed one are committed, while everything before the error, migrations included, is rolled back:
- a dependency resolved by a retry (`a.sql` with `set xact_abort on` reads a view of `c.sql`): `c.sql` and the retry of `a.sql` succeed in autocommit, and the deployment fails at the end with `TransactionAbortedException: The transaction has aborted.`, without the cause. The database keeps the views and their `applied` rows, but not the table of the migration and not its row.
- an error a retry cannot fix (`set xact_abort on; select 1/0;`): the deployment fails with the error of the file, but the code files after it are committed.

The deployment does not report success, but `TransactionWrapLevel: Deployment` no longer means all or nothing: new code can be left on the old schema.

Found by the code review of 2026-11-08 (finding 4).

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:50-58` (`CanRetry`)
- `src/DbLive/Deployers/Code/CodeDeployer.cs:184-204` (the retry, the `Debug` log of an attempt)
- `src/DbLive/Adapter/IDbLiveDA.cs` (`ErrorAbortsTransaction`), `src/DbLive.MSSQL/MsSqlDA.cs`

## Fix
- [x] Reproduce first: an integration test on SQL Server, `TransactionWrapLevel: Deployment`, a code file `set xact_abort on; create view ... from dbo.Missing`. What does the deployment fail with?
- [x] Which fix? `Transaction.Current.TransactionInformation.Status` does not work (it stays `Active`), and neither does `Transaction Binding=Explicit Unbind`. `select @@trancount` in the ambient transaction does: 0 after the rollback, 1 otherwise. Proposed: after a failed code file in a transaction, ask the adapter whether the transaction is still open (SQL Server: `@@trancount`, PostgreSQL: never), and when it is not, do not retry and fail at once with the error of the file. Decided by the maintainer on 2026-11-08: as proposed.

## Docs to update
- `docs/llms-full.txt` §6 (order and retries), §16 (provider differences), §17 (`CodeDeploymentAggregateException`)
- `docs/deployment/code.md` ("Order, parallelism and retries", "Transactions and timeouts")

## Done when
- [x] Reproduced, or closed as not reproducible with the test kept
- [x] Fixed, with the integration test on SQL Server
- [x] Docs updated

## Notes
- 2026-11-08, verified on SQL Server 2025 (Testcontainers), a temporary experiment test (not kept; `CodeRetryInDeploymentTransactionTests` replaces it). In a `TransactionScope`: a table is created, then the failing script, then `create table dbo.AfterError`, then the scope is disposed without `Complete`.

  | Script | `@@trancount` after it | `dbo.AfterError` after the rollback |
  | --- | --- | --- |
  | `create view dbo.V as select x from dbo.Missing;` | 1 | no |
  | `set xact_abort on; drop proc dbo.Missing;` | 1 | no |
  | `set xact_abort on;` `go` `create view dbo.V as select x from dbo.Missing;` | 0 | committed |
  | `set xact_abort on;` `go` `create view dbo.V as select missing_col from ( select 1 as a ) t;` | 0 | committed |
  | `set xact_abort on; select 1/0;` | 0 | committed |
  | `select convert(int, 'abc');` (no `xact_abort`) | 0 | committed |
  | `rollback;` | 0 | committed |

  `Transaction.Current.TransactionInformation.Status` is `Active` in every case. `Transaction Binding=Explicit Unbind` in the connection string changes nothing: DbLive opens a new connection for each command.
- The first guess of this task was wrong: a retry does not fail on the rolled-back transaction, it runs and is committed outside it. `drop proc` of a missing procedure does not roll back the transaction even with `xact_abort on`; that is why the test of 0096-51 passed.
- An explicit `rollback` or `commit` in a script has the same effect on every later script, not only on retries. That is a mistake in the script, not covered by this task.
- Migrations and breaking scripts write nothing after their error, so they are not affected: the deployment stops and is rolled back.
- The check runs only after a failed code file in a transaction: one `select @@trancount`. When the check itself fails (a lost connection), its error leaves the deployment uncaught, like the write of an error row outside a transaction (0096-51).

## Result
- `IDbLiveDA.IsTransactionUsableAfterErrorAsync()` replaces `ErrorAbortsTransaction` (not released): SQL Server `select @@trancount > 0`, PostgreSQL `false`.
- `CodeItemDeployer` asks it after a failed code file in a transaction. When the transaction is ended, `CanRetry` is false and the message of `CodeDeploymentException` ends with "The error ended the deployment transaction, so the file is not retried.". `CodeDeployer` logs such a file as "not retried" instead of "after 1 attempts".
- Tests: `CodeRetryInDeploymentTransactionTests` on SQL Server, a missing dependency fixed by a retry, and three errors that roll back the transaction (`xact_abort` with a missing dependency, `xact_abort` with `1/0`, a failed conversion): the deployment fails at once with the error of the file, and neither the migration before it nor the file after it is committed. Without the fix the three fail (verified): `TransactionAbortedException`, or the file after it committed. `CodeItemDeployerTests` and the PostgreSQL test of a failed code file in the deployment transaction check the new message.
- Docs: `docs/llms-full.txt` §6, §16, §17; `docs/deployment/code.md`.
