---
type: bug
priority: high
created: 2026-11-25
closed: 2026-11-25
breaking: true
---
# SQL Server: a script that leaves a transaction open is rolled back without an error

## Release note
**SQL Server: a script that leaves a transaction open now fails.** A `BEGIN TRANSACTION` without `COMMIT` in a script, or in a procedure it calls, raised no error: the commit of DbLive only lowered `@@trancount`, the connection went back to the pool with the transaction open, and SQL Server rolled it back later, when the session was reused. The deployment succeeded without what the script did: a migration stayed unapplied and ran again on every deployment, and a code file was recorded as applied without its objects, so the next deployments skipped it. DbLive now compares `@@trancount` before and after every script, also a script without a transaction (`TransactionWrapLevel: None`, BeforeDeploy, AfterDeploy, the breaking scripts of the Code folder), and fails it with its file and `The script left a transaction open: ...`. It rolls the transaction back at once: nothing of a script in a transaction of DbLive stays; a script without one keeps what it ran before `BEGIN TRANSACTION`. Such a code file is not retried. A script without a transaction that fails with an error no longer leaves its open transaction and its locks in the connection pool. A SQL test that leaves a transaction open, also in a procedure it calls, fails with `The test left a transaction open: ...`.

To upgrade:
- Commit every transaction that a script begins, on every path, also in the procedures it calls. A script that runs in a transaction of DbLive needs no `BEGIN TRANSACTION`. Such a script deployed before and lost what it did; now it fails every time.
- A SQL test with `begin transaction` without `commit` passed; it now fails.

## Problem
The check of the transaction (§10) finds a script that ends the transaction of DbLive, by the ID of the transaction. It does not find a script that leaves a transaction of its own open (`begin tran` without `commit`, or a procedure that does it): the ID stays the same and `@@trancount` is not compared. The `commit` of DbLive or SqlClient then only lowers `@@trancount`, the session goes back to the pool with the transaction open, and SQL Server rolls it back when the session is reused (`sp_reset_connection`). The deployment succeeds, and what the script did is gone.

Verified by the review of 2026-11-25 (a scratch app on the source projects, SQL Server 2025, a database per case, checked afterwards on a connection outside the pool):
- **E1, migration** with `Migration`: `begin tran; create table dbo.t1 ...` without `commit`. `DeployAsync` succeeds; `dbo.t1` does not exist, the row in `dblive.migration` is `none`. Every next deployment "succeeds" the same way, and the migration is never applied.
- **E2, code file** `create or alter proc dbo.p1 ... GO begin tran`: the deployment succeeds, the row in `dblive.code` is `applied`, `dbo.p1` does not exist. The next deployment skips the file (`[hash match]`): the procedure stays missing.
- **E3, a regression of 2.0:** a migration that calls a procedure that runs `begin tran` without `commit`. SQL Server fails it with Msg 266 ("Transaction count after EXECUTE indicates a mismatching number of BEGIN and COMMIT statements"); with `set implicit_transactions on`, which the check turns on for the script, there is no Msg 266 (control query: `@@trancount` 3). Through DbLive the deployment succeeds and the migration is lost, as in E1.
- **E7, SQL test** `exec dbo.p_open;` (a procedure that leaves a transaction open): `RunTestAsync` returns `IsSuccess = true`. Without `IMPLICIT_TRANSACTIONS` SQL Server fails the test with Msg 266, so a SQL test no longer finds this bug of a procedure.

By the same mechanism, not run: a BeforeDeploy or AfterDeploy script, a breaking script of the Code folder and a migration with `TransactionWrapLevel: None` that leave a transaction open; the migration with `None` is marked `applied` on another connection, and its changes are rolled back.

The deployment test does not find it reliably: the object is missing from both schema snapshots, so the schema check sees no difference; only a SQL test that uses the object fails, and E7 shows that a test of the faulty procedure itself passes.

It is a bug of a script, of the same kind as the `COMMIT` or `ROLLBACK` that DbLive finds since 0096-87; in E3 and E7 the check of 2.0 turns the Msg 266 of SQL Server into a silent loss. 0096-87 covers `rollback; begin tran` (another ID), not a transaction left open.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:351` (`StartTransactionGuardSql`: the ID, then `set implicit_transactions on`), `:356-361` (`EndTransactionGuardSql`: compares the ID only).
- `:332-345` (`ExecuteInOwnTransactionAsync`, the own transaction of a code file, `commit transaction` at `:338`).
- `:226-229`: a script without a transaction, no check.

## Fix
- In a transaction: read `@@trancount` together with the ID before the script, and fail in the end guard when it is higher after the script: roll back, throw an error that says the script left a transaction open (`BEGIN TRANSACTION` without `COMMIT`). A nested transaction cannot be rolled back alone, so the whole transaction goes, as for a script that ended it.
- Without a transaction: after the script, `@@trancount > 0` → roll back and fail the script.
- The SQL test: the same check by `@@trancount` before and after the test. It runs through the same guard as a script in a transaction (`ExecuteGuardedAsync`), so it comes with the first item.
- [x] PostgreSQL: a script without a transaction (`None`, BeforeDeploy) that runs `begin;` without `commit` leaves the session in a transaction block, and Npgsql rolls it back when the connection goes back to the pool? In a transaction of DbLive `begin` is only a warning, nothing to do. Yes, verified 2026-11-25 (Notes): the deployment succeeds, and nothing of the file stays.
- [x] PostgreSQL: fix it the same way in this task, or keep it a documented limitation (§10, §16, §19 say so now)? The fix: after a script without a transaction, `select pg_current_xact_id_if_assigned() is not null` on its connection; true → `rollback;` and `DbLiveTransactionLeftOpenException`. One round trip per script without a transaction; a block that wrote nothing is not found, and it loses nothing. Decision of the maintainer (2026-11-25): a task of its own, `0096-181-postgresql-open-transaction-rolled-back-silently.md`. Until it is done, the docs describe the PostgreSQL case as a limitation.

## Docs to update
- `docs/llms-full.txt` §0 rule 22 (new), §8.2 ("A test must not leave a transaction open"), §10 ("A script must not leave a transaction open"), §13.3, §16, §17 (the two new errors), §19
- `docs/deployment/migrations.md` ("No transaction left open", new), `docs/deployment/index.md`, `docs/deployment/code.md` ("Order, parallelism and retries")
- `docs/unit-testing/index.md` ("Code that leaves a transaction open", new), `docs/unit-testing/deployment-tests.md`

## Done when
- [x] Fixed for scripts in a transaction, without one and for SQL tests, with integration tests on SQL Server (E1, E2, E3, E7)
- [x] PostgreSQL checked
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-25. The experiments: `scratchpad/mssql/exp/Program.cs` of the review session (`e1`, `e2`, `e3`, `e266`, `e7`), not kept in the repository.
- 2026-11-25, PostgreSQL (verified, PostgreSQL 18 of the integration tests, a temporary test, not kept). The table `t` is empty before the script; rows read on a connection with `Pooling=false`:
  - Npgsql alone, one command on a pooled connection: `insert 1; begin; insert 2;` leaves no row after `Close()`, also not row 1: `BEGIN` turns the implicit transaction of the command into a transaction block, and the statements before it are in it. `begin; insert 1;` the same. Npgsql rolls the block back when the connection goes back to the pool, before the connection is reused. On the same connection after the script `pg_current_xact_id_if_assigned()` is the ID of the block when it wrote (755, 769), null after `begin; select 1;` and after `insert 1;` alone. `now() = statement_timestamp()` is false in every case, also without a block: no use for the check.
  - Through DbLive, `TransactionWrapLevel: Migration`: the migration `002` with `"TransactionWrapLevel": "None"` (`begin; insert 1;`, and `insert 1; begin; insert 2;`), then `003` `insert 3`. The deployment succeeds, `002` is `applied`, the rows are `3` only.
  - A BeforeDeploy or AfterDeploy script `begin; insert 10 ...;` without the deployment transaction: the deployment succeeds, no row.
- The fix (SQL Server): `StartTransactionGuardSql` reads `current_transaction_id()` and `@@trancount`; `EndTransactionGuardSql` returns 0 (kept), 1 (ended, the ID differs) or 2 (left open, the same ID and a higher `@@trancount`), and rolls back on 1 and 2. A script without a transaction runs through `ExecuteWithoutTransactionAsync`: after it `if @@trancount = 0 select 0; else begin rollback; select 1; end;`. The new `DbLiveTransactionLeftOpenException` (internal, `DbLiveSqlException`) takes what DbLive rolled back, which differs with and without a transaction. `CodeItemDeployer` does not retry it ("The file left a transaction open, so it is not retried."); `UnitTestItemRunner` fails the test with `TransactionLeftOpenMessage`. A second exception type, not `DbLiveTransactionEndedException` with another text: the messages of the deployers and of the test speak of COMMIT or ROLLBACK for that one.
- Also after an error of a script without a transaction DbLive rolls back a transaction it left open, at once (`TryRollbackAsync`, as for the own transaction of a code file). Before, the pooled session kept the transaction and its locks until it was reused: `begin tran; insert 1; select 1 / 0;` through `ExecuteNonQueryAsync`, then `set lock_timeout 0; select count(*) from dbo.T` on a connection outside the pool failed with the lock timeout (verified with the fix turned off). A code file with In-Memory OLTP DDL that fails is retried on another session of the pool, which those locks would block.
- In a transaction an error of the script with a transaction left open now rolls back the whole transaction too (the end guard runs after the error as before). Before, a code file in the deployment transaction that failed with `@@trancount` 2 was retried in it, and the commit of the deployment only lowered the count: the whole deployment was rolled back when the session was reused. Not run before the fix; it follows from E1.
- Tests (verified): `src/DbLive.MSSQL.Tests/ScriptLeavesTransactionOpenTests.cs`, 17 cases. With the check turned off (the end guard returns 0 for a higher `@@trancount`, no check and no rollback without a transaction) all fail but `Test_whose_transactions_are_balanced_passes`. Unit tests of `CodeItemDeployer` and `UnitTestItemRunner` for the new exception. The whole SQL Server suite (196) and `DbLive.Tests` (705) pass.
- The release note of `0096-87`, closed since 1.5.0, stays right: it describes the check of the transaction ID and does not mention Msg 266, which `IMPLICIT_TRANSACTIONS` of that task suppressed and this task makes a failure again.

## Result
- SQL Server: the guard of a script in a transaction (`ExecuteGuardedAsync`: migrations and breaking scripts, code files in their own transaction and in the deployment transaction, BeforeDeploy and AfterDeploy with `Deployment`, the self-deployment, SQL tests and `setup.test.sql`) compares `@@trancount` besides the ID of the transaction, and rolls back the whole transaction when the script left one open. A script without a transaction (`ExecuteWithoutTransactionAsync`) is checked after it, and its open transaction is rolled back also after an error.
- New internal `DbLiveTransactionLeftOpenException`; `CodeItemDeployer` does not retry it, `UnitTestItemRunner` fails the test with `TransactionLeftOpenMessage`.
- PostgreSQL checked by an experiment: the same loss without a transaction of DbLive. Split out to `0096-181`; the docs describe it as a limitation.
- Tests: `ScriptLeavesTransactionOpenTests` (SQL Server, 17 cases, E1, E2, E3, E7 and the scripts without a transaction), unit tests of `CodeItemDeployer` and `UnitTestItemRunner`.
- Docs: `docs/llms-full.txt` §0 rule 22, §8.2, §10, §13.3, §16, §17, §19; `docs/deployment/migrations.md`, `index.md`, `code.md`; `docs/unit-testing/index.md`, `deployment-tests.md`.
