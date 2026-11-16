---
type: bug
priority: medium
created: 2026-11-16
---
# A timeout of a `TransactionScope` does not fail as `ExecutionTimeoutException`

## Problem
0096-84 made the timeout of a script one error on both providers, `ExecutionTimeoutException`. It covers the command timeouts only. DbLive has a second timer, the timeout of its `TransactionScope`:
- `DeploymentTimeout`, with `TransactionWrapLevel: Deployment`;
- the `Timeout` of a migration or breaking script with `TransactionWrapLevel: Migration` (the default): the same value as the timeout of its script;
- `UnitTestItemTimeout`: the transaction of a test, and also the timeout of `setup.test.sql` and of the test, each on its own;
- the self-deployment of DbLive (5 minutes; its migrations 30 seconds each).

When this timer runs out, System.Transactions aborts the transaction on its timer thread. It does not interrupt the running command. The error comes from the next step in the scope, in a shape that depends on that step:
- opening the next connection: `TransactionException` "The operation is not valid for the state of the transaction." with `TimeoutException` "Transaction Timeout" inside (verified for `DeploymentTimeout` in 0096-68);
- the next batch on the same connection: not verified;
- the write of a row in the `dblive` tables: not verified;
- `Complete()` and `Dispose()` of the scope: `TransactionAbortedException` (docs §17; not verified here).

The error does not say which timeout ran out, and for a script it is not `ExecutionTimeoutException`. So "a timeout always looks the same" (0096-84) does not hold for these cases.

A migration in its own transaction (the default) has both timers at the same value. The scope starts before the script: before the connection opens, the isolation level is set and the first batch starts. So its timer runs out first, also because the command timeout of a batch is rounded up to whole seconds. A script of many short batches can therefore fail with the error of the scope instead of `ExecutionTimeoutException`. A single long statement fails with `ExecutionTimeoutException` (docs §17; to verify).

Not known: what the batches after the abort do on SQL Server. Does the next batch on the same connection fail, or run in autocommit mode and commit? (`MsSqlDA.IsTransactionUsableAsync` notes that after the server rolled back a transaction, the next commands of the scope run in autocommit mode.) If they commit, a migration that timed out is applied in part while its row is not `applied`, and the next deployment runs it again.

## Where
- `src/DbLive/Common/Transactions.cs` (`TransactionRunner`), `src/DbLive/Common/TransactionScopeManager.cs`
- `src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs` (the scope of a migration, with its `Timeout`)
- `src/DbLive/Deployers/DbLiveDeployer.cs` (`DeploymentTimeout`)
- `src/DbLive/Deployers/Testing/UnitTestItemRunner.cs` (the scope of a test, with `UnitTestItemTimeout`; the same value for `setup.test.sql` and the test)
- `src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs`
- `src/DbLive/Deployers/Code/CodeItemDeployer.cs` (0096-68: the error of a code file when the deployment transaction ends under it)

## Fix
Start with the experiment (see Notes), then decide.

Proposal of the agent (2026-11-16), to decide after the experiment: one timer counts the time of an item. A migration and a test are limited by the deadline of DbLive (`ScriptTimeout` of 0096-84), which fails with `ExecutionTimeoutException`. Their scope gets no timeout, or one later than the script as a backstop, so that it does not race with the script. A test gets one deadline for `setup.test.sql` and the test together, not a timeout for each. `DeploymentTimeout` stays a timeout of the transaction, because it also covers the work of DbLive between the scripts; DbLive turns its error into one that names `DeploymentTimeout`.

- [ ] What does each scope throw today when its timer runs out, on both providers? The experiment in Notes.
- [ ] Does a batch after the abort run in autocommit mode on SQL Server and commit? If it does, it is a bug of its own, more urgent than the shape of the error.
- [ ] The scope of a migration and of a test: no timeout (the deadline of the script only), or a timeout later than the script, and how much later?
- [ ] `setup.test.sql` and the test: one deadline for both, or each its own `UnitTestItemTimeout` as now?
- [ ] `DeploymentTimeout`: which error, and where is it caught? `ExecutionTimeoutException` does not fit: no script ran too long, the deployment did. An exception of its own, or the error of today with a message that names `DeploymentTimeout`?
- [ ] `breaking: true`? It depends on the answers: the error of a timeout changes, and a test whose `setup.test.sql` and test together run longer than `UnitTestItemTimeout` would fail.

## Docs to update
- `docs/llms-full.txt` §8.2 (the transaction of a test), §10 (transaction timeouts; "the transaction timeout above is a timer of its own"), §17 (`TransactionAbortedException`; `CodeDeploymentAggregateException` with `DeploymentTimeout`)
- `docs/project/configuration.md` ("Timeouts")

## Done when
- [ ] The shapes of today verified on SQL Server and PostgreSQL: `DeploymentTimeout`, the `Timeout` of a migration with `Migration` (one long statement, many short batches), `UnitTestItemTimeout` (`setup.test.sql` and the test)
- [ ] The questions above answered by the maintainer
- [ ] Fixed, with integration tests
- [ ] Docs updated

## Notes
- Came up 2026-11-16 in 0096-84. Decision of the maintainer (2026-11-16): 0096-84 handles the command timeouts of scripts only; the timeouts of a `TransactionScope` are this task.
- Why 0096-84 did not do it: the error comes from the next step, not from one call that DbLive can wrap. The step differs (a connection, a batch, a row in `dblive`, the end of the scope), and so does the shape. The data access does not know which timeout ran out. And the timer of the scope does not interrupt the running command.
- The experiment: for each scope (the deployment, a migration with `Migration`, a test, the self-deployment) and each provider, run a script of one long statement and a script of many short batches whose sum runs longer than the timeout. Record the exception chain, the step that threw, what stays committed afterwards, and whether the session is left with an open transaction.
- The integration tests of 0096-84 (`CommandTimeoutTests`) use `"TransactionWrapLevel": "None"` for the migration on purpose: without a transaction only the timeout of the script applies.
