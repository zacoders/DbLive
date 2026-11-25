---
type: bug
priority: high
created: 2026-11-25
---
# SQL Server: a script that leaves a transaction open is rolled back without an error

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
- The SQL test: the same check by `@@trancount` before and after the test.
- [ ] PostgreSQL: a script without a transaction (`None`, BeforeDeploy) that runs `begin;` without `commit` leaves the session in a transaction block, and Npgsql rolls it back when the connection goes back to the pool? In a transaction of DbLive `begin` is only a warning, nothing to do. Not run.

## Docs to update
- `docs/llms-full.txt` §0 rule 21, §10 ("A script must not end the transaction it runs in"), §16, §17 (the new error)
- `docs/deployment/migrations.md` ("No COMMIT or ROLLBACK in a script")

## Done when
- [ ] Fixed for scripts in a transaction, without one and for SQL tests, with integration tests on SQL Server (E1, E2, E3, E7)
- [ ] PostgreSQL checked
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-25. The experiments: `scratchpad/mssql/exp/Program.cs` of the review session (`e1`, `e2`, `e3`, `e266`, `e7`), not kept in the repository.
