---
type: feature
priority: medium
created: 2026-11-09
closed: 2026-11-14
---
# The transaction check hides the error of a code file (SQL Server); code files are not retried in the transaction on PostgreSQL

## Release note
**PostgreSQL: code files are retried in the deployment transaction.** With `TransactionWrapLevel: Deployment`, an error of a code file aborted the PostgreSQL transaction, so a view or a `language sql` function that uses an object of a file deployed after it failed the deployment. Each code file now runs under a savepoint in the deployment transaction: after an error DbLive rolls back to it, which also undoes the statements of the failed attempt, and the file is retried as without a transaction. A file that fails on every attempt fails the deployment after `MaxCodeDeployRetries` attempts, and nothing is committed. Each savepoint is a PostgreSQL subtransaction: a deployment that runs more than 64 code files (attempts included) in one transaction slows down visibility checks of other sessions on a busy server and its replicas while it runs.

**The error of a code file is kept when the deployment transaction ends under it.** A code file that broke its connection (an error of severity 20 or more on SQL Server) or outlived `DeploymentTimeout` failed the deployment with a bare `TransactionException: The operation is not valid for the state of the transaction.`, without the path of the file and its error. It now fails with `CodeDeploymentAggregateException` that names the file and contains its error. Without a transaction, a failed write of the error row (the server is unreachable) no longer replaces the error of the file: it is logged as a warning.

Nothing to change on upgrade.

## Problem
When a code file fails inside the deployment transaction (`TransactionWrapLevel: Deployment`), `CodeItemDeployer` asks the provider in its `catch` whether the transaction can still be used (`IsTransactionUsableAfterErrorAsync`, added by 0096-61). The call is not protected.

On SQL Server the check opens a new connection, which enlists in `Transaction.Current`. When the error aborted the `System.Transactions` transaction itself (the connection broke on an error of severity 20 or more, or the `TransactionScope` timed out after `DeploymentTimeout`), opening the connection throws `TransactionException` ("The operation is not valid for the state of the transaction"). The exception leaves the `catch`, so the file gets no `CodeItemDeployResult`: `Task.WhenAll` in `CodeDeployer.DeployGroupAsync` rethrows the bare `TransactionException`. The user gets no `CodeDeploymentAggregateException`, no path of the file and no SQL error, and none of them is logged.

Verified on SQL Server 2022 (see Notes): both cases throw.

The branch without a transaction has the same shape: it writes the error row with `SaveCodeItemAsync` in the `catch`, and a lost connection makes the write throw and replace the error of the file. That code is older (it is in 1.5.0).

Found by the code review of 2026-11-09 (finding 4).

### PostgreSQL: no retries in the deployment transaction
The check does not throw on PostgreSQL: its `IsTransactionUsableAfterErrorAsync` returns `false` without a query, because every error aborts a PostgreSQL transaction. So with `Deployment` a failed code file is never retried, and retries are how DbLive resolves the order of dependencies between code files. A view or a `language sql` function that uses an object of a later file fails the deployment at once; with `None` the same project deploys (verified, see Notes). Today the only way around it is `CodeSubFoldersDeploymentOrder`.

## Where
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs:51-63` (the check), `:70` (the write of the error row)
- `src/DbLive.MSSQL/MsSqlDA.cs:13-19` (`IsTransactionUsableAfterErrorAsync`)
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:16-17` (`IsTransactionUsableAfterErrorAsync`: always `false`)
- `src/DbLive.PostgreSQL.Tests/CodeBreakingTests.cs:233` (a failed code file in the deployment transaction: expects "not retried")

## Fix
### The check
Wrap the check in `try`/`catch`: an exception of the check means the transaction cannot be used, so `CanRetry = false`, and the result keeps the error of the file. Log the exception of the check as a warning.

- [x] Also protect the write of the error row in the branch without a transaction, so the error of the file stays the result? Decided by the maintainer on 2026-11-14: yes.

### PostgreSQL: a savepoint per code file
Decided by the maintainer on 2026-11-14: on PostgreSQL a code file in the deployment transaction runs under a savepoint, so its error no longer ends the transaction and the file is retried as on SQL Server.

- Only code files (`CodeItemDeployer`), only in a transaction. Migrations, folder scripts and Code breaking scripts are not retried and get no savepoint.
- The savepoint is a detail of the PostgreSQL provider, not of the deployer. Decided by the maintainer on 2026-11-14, after a first version with three `IDbLiveDA` members (`SetSavepointAsync`, `ReleaseSavepointAsync`, `RollbackToSavepointAsync`, a no-op on SQL Server) that the deployer called in order: one member `ExecuteCodeItemAsync(sql, isolationLevel, timeout)` instead. Its contract: inside the ambient transaction an error of the file leaves the transaction usable when the provider can.
- PostgreSQL, in a transaction: `savepoint`, the file, `release savepoint`. After an error: `rollback to savepoint`, and the error of the file is thrown. When the rollback throws (a broken connection, the transaction timed out), its exception is dropped, so the error of the file stays the one thrown. Without a transaction: `ExecuteNonQueryAsync`. The name of the savepoint is one a user script would not take (`dblive_code_item`), and one name serves all files: code in the deployment transaction runs on one thread (`CodeDeployer` starts one worker), and each file releases or rolls back its savepoint.
- The release comes before the `applied` row, not after it. When the write of the row fails on PostgreSQL, the transaction is aborted outside the savepoint, the check says it is not usable, and the deployment fails with "not retried". That is right: a failed write to `dblive.code` is not a missing dependency.
- `CodeItemDeployer` calls `ExecuteCodeItemAsync`, and after an error in a transaction only the check, in a `try`/`catch` (see "The check"). The check stays a member of its own without side effects: 0096-76 calls it after every test, also after a test without an error. On PostgreSQL it becomes a query instead of `false`: a statement in an aborted transaction fails with `25P02`, so `false` then, `true` otherwise (verified by the integration test).
- The savepoint commands are separate commands, not text added to the file: a file that ends with a `--` comment without a line break would swallow an added statement. Every `NpgsqlConnection` opened in the scope gets the same session (verified), so separate commands work.
- A failed file leaves nothing behind: the rollback undoes its statements that ran before the error, as the implicit transaction of a file outside a transaction does now.
- The message "The error ended the deployment transaction, so the file is not retried." stays only for a failed rollback to the savepoint.
- SQL Server gets no savepoint: without `XACT_ABORT` an error leaves the transaction open, with it the whole transaction is rolled back (`@@trancount` 0, verified in 0096-61), and there is no savepoint to return to.
- Each savepoint of a file that changes something is a subtransaction with its own XID. More than 64 in one transaction overflow the subtransaction cache of the backend, and other sessions then read `pg_subtrans` to check row visibility: a known slowdown of a busy server and its replicas while the deployment runs. From PostgreSQL internals, not verified. To mention in the docs.

- [x] The check changes its meaning on PostgreSQL here, and 0096-76 gives it a second caller. Settle the rename that 0096-76 asks about in this task? Proposed: `IsTransactionUsableAsync`. Decided by the maintainer on 2026-11-14: yes, `IsTransactionUsableAsync`.
- [x] `type: bug` gives no minor version, but the PostgreSQL part is new behavior that users see: a project that failed with `Deployment` now deploys. Change the type to `feature`, so that the PR title gets `feat:`? Decided by the maintainer on 2026-11-14: yes.

## Docs to update
- `docs/llms-full.txt`: retries of code files ("On PostgreSQL every error does that", now line 328), code items in the deployment transaction (331), the table of provider differences (957), `CodeDeploymentAggregateException` in the table of exceptions (978). Add that `set xact_abort on` in a script does not reach the later scripts (verified, see Notes).
- `docs/deployment/code.md`: retries in the deployment transaction (now lines 77 and 90).

## Done when
- [x] Reproduced on SQL Server, or ruled out: a code file that breaks its connection (`raiserror(..., 20, 1) with log`) in a deployment transaction
- [x] Fixed, with a unit test: a data access whose `IsTransactionUsableAfterErrorAsync` throws, and the deployment fails with `CodeDeploymentAggregateException` that names the file
- [x] PostgreSQL, integration test: with `Deployment`, a view and a `language sql` function that use an object of a later file are deployed through a retry
- [x] PostgreSQL, integration test: a file whose first attempt fails after some of its statements ran (`create table ...; create view ... as select ... from <object of a later file>;`) succeeds on the retry: the rollback to the savepoint undid the table
- [x] PostgreSQL, integration test: a file that fails on every attempt fails the deployment after `MaxCodeDeployRetries` attempts, and nothing is committed (the test in `CodeBreakingTests` moved to `CodeRetryInDeploymentTransactionTests`)
- [x] Docs updated

## Notes
Reproduced on 2026-11-14 with a temporary integration test (SQL Server 2022 in Testcontainers; the test is not kept). Project: `001.migration.sql` and `Code/a.sql`, verified:

| `Code/a.sql` | Settings | What `DeployAsync` throws |
| --- | --- | --- |
| `raiserror('boom', 20, 1) with log;` | `TransactionWrapLevel: Deployment` | `TransactionException`: "The operation is not valid for the state of the transaction." → `TransactionPromotionException` → `InvalidOperationException`: "The requested operation cannot be completed because the connection has been broken." |
| `waitfor delay '00:00:06';` | `TransactionWrapLevel: Deployment`, `DeploymentTimeout: 00:00:03` | `TransactionException`: "The operation is not valid for the state of the transaction." → `TimeoutException`: "Transaction Timeout" |
| `raiserror('boom', 20, 1) with log;` | `TransactionWrapLevel: None` | `CodeDeploymentAggregateException` with `Code/a.sql` and "boom", after 5 attempts: the write of the error row opens a new pooled connection and succeeds. Not affected. |

In the first two cases the stack ends in `MsSqlDA.IsTransactionUsableAfterErrorAsync` (`using var cnn = GetConnection()`: enlisting the new connection in `Transaction.Current`), called from `CodeItemDeployer.DeployAsync:53`. Neither the path of the file nor "boom" is in the exception or in the log.

From the code, not verified: the branch without a transaction fails only when the write of the error row itself cannot connect (the server is unreachable). The error that replaces the error of the file is then a connection error, so the cause is still visible; only the path of the file is lost.

PostgreSQL, verified on 2026-11-14 with a temporary integration test (Testcontainers; not kept). Project: a migration, `Code/a.sql` that uses an object of `Code/c.sql`:

| `Code/a.sql` | `Deployment` | `None` |
| --- | --- | --- |
| `create view a as select x from c;` | fails at once: `42P01 relation "c" does not exist`, "not retried", nothing committed | deploys through a retry |
| `create function fa() returns int language sql as $$ select fc() $$;` | fails at once: `42883 function fc() does not exist` | not run |
| `create function fa() returns int language plpgsql as $$ begin return fc(); end $$;` | deploys: the body is resolved at run time | not run |

Savepoints in the deployment transaction, verified the same day: in a `TransactionScope` from `TransactionScopeManager.Create`, each command on a new `NpgsqlConnection`: `create table m`, `savepoint code_file`, `create view a as select x from c` (fails with `42P01`), `rollback to savepoint code_file`, `create view c`, `create view a`, `Complete()`. After the commit `a`, `c` and `m` exist, and `pg_backend_pid()` is the same for all commands. It works the same with `savepoint code_file; <file>; release savepoint code_file;` sent as one command.

SQL Server: `set xact_abort on` in a migration does not reach the later scripts, verified on 2026-11-14 with a temporary integration test (not kept). `001.migration.sql` sets `xact_abort on` in its second batch; its batches, `002.migration.sql` and a code file record `@@options & 16384`. With `Deployment`, `Migration` and `None` the option is on in the batches of `001` after the `set` and off in `002` and in the code file, with the same `@@spid`. With `Deployment`, a code file with a missing dependency is still retried after it, and the deployment succeeds. Each script opens its own `SqlConnection`, and the pool resets the session state when the next script opens the connection; one connection for the whole deployment would carry the option to all later scripts.

## Result
- `IDbLiveDA.IsTransactionUsableAfterErrorAsync()` is renamed to `IsTransactionUsableAsync()` (not released) and has no side effects. SQL Server: `@@trancount > 0`, as before. PostgreSQL: `select 1`, `false` when it fails with `25P02`, instead of the constant `false`.
- New `IDbLiveDA.ExecuteCodeItemAsync(sql, isolationLevel, timeout)`. PostgreSQL in a transaction: `savepoint dblive_code_item`, the file, `release savepoint`, each a separate command; after an error `rollback to savepoint`, whose own exception is dropped, and the error of the file is thrown. SQL Server, and PostgreSQL without a transaction: `ExecuteNonQueryAsync`.
- `CodeItemDeployer` runs the file with `ExecuteCodeItemAsync`. After an error in a transaction: the check in a `try`/`catch`; an exception there means the transaction is not usable (`CanRetry = false`, the message says the file is not retried), it is logged as a warning, and the result keeps the error of the file. Without a transaction a failed write of the error row is logged as a warning and the error of the file stays the result.
- Release note of 0096-61 (same release): "also on PostgreSQL" removed from the "not retried" message.
- Docs: `docs/llms-full.txt` (retries, code in the deployment transaction with the subtransaction note, `xact_abort` stays in its file, provider differences, `CodeDeploymentAggregateException`), `docs/deployment/code.md`.
- Unit tests (`CodeItemDeployerTests`): the file runs through `ExecuteCodeItemAsync`, not `ExecuteNonQueryAsync`; no check without a transaction; a throwing check keeps the error of the file and logs a warning; a failing write of the error row keeps the error of the file. `CodeDeployerTests`: with the real `CodeItemDeployer` and a throwing check the deployment fails with `CodeDeploymentAggregateException` that names the file.
- Integration tests, SQL Server (`CodeRetryInDeploymentTransactionTests`): `raiserror('boom', 20, 1) with log` and a file that outlives `DeploymentTimeout` fail with the path of the file, nothing committed; the inner exception is the SQL error and `TransactionException` with `TimeoutException`. PostgreSQL (new `CodeRetryInDeploymentTransactionTests`): a view and a `language sql` function deployed through a retry; the statements of a failed attempt rolled back before the retry; a file that fails on every attempt fails without "not retried", nothing committed; `pg_terminate_backend(pg_backend_pid())` fails with `57P01` and "not retried"; `IsTransactionUsableAsync` is `false` after an error of `ExecuteNonQueryAsync`, and `true` after an error of `ExecuteCodeItemAsync`, which also undid the statements of its file.
- All tests pass: `DbLive.Tests` 472, `DbLive.PostgreSQL.Tests` 55, `DbLive.MSSQL.Tests` 74.
