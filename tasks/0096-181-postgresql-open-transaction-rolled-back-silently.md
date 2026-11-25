---
type: bug
priority: medium
created: 2026-11-25
---
# PostgreSQL: a script without a transaction that leaves one open is rolled back without an error

## Problem
A PostgreSQL script that runs without a transaction of DbLive (`TransactionWrapLevel: None`, BeforeDeploy and AfterDeploy and the breaking scripts of the Code folder without `Deployment`) and runs `BEGIN` without `COMMIT` leaves its session in a transaction block. Npgsql rolls the block back when the connection goes back to the pool, without an error. The deployment succeeds, a migration is recorded as `applied`, and nothing of the file stays: `BEGIN` turns the implicit transaction of the command into the block, so the statements before it in the same file are rolled back too.

Verified on 2026-11-25 (PostgreSQL 18 of the integration tests, a temporary test, not kept; details in the Notes of `0096-178`):
- the migration `002` with `"TransactionWrapLevel": "None"`, `begin; insert 1;` or `insert 1; begin; insert 2;`, then `003` `insert 3`: the deployment succeeds, `002` is `applied`, the table has the row `3` only;
- a BeforeDeploy or AfterDeploy script `begin; insert 10 ...;` without the deployment transaction: the deployment succeeds, no row.

In a transaction of DbLive there is nothing to fix: `BEGIN` is only a warning there, and the script runs in that transaction.

The same bug on SQL Server is fixed by `0096-178`: DbLive checks `@@trancount` after a script without a transaction, rolls back and throws `DbLiveTransactionLeftOpenException`. The docs describe the PostgreSQL case as a limitation.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`, `ExecuteScriptAsync`: the branch without a transaction runs `ExecuteCommandAsync` with no check after it.
- `src/DbLive/Adapter/DbLiveTransactionLeftOpenException.cs`: its message says `BEGIN TRANSACTION` and "a procedure it called", from SQL Server.

## Fix
- After a script without a transaction, on its connection: `select pg_current_xact_id_if_assigned() is not null`. True → `rollback;` and `DbLiveTransactionLeftOpenException`. One round trip per script without a transaction. Verified in the experiment: the ID is set after `begin; insert ...`, null after `begin; select 1;` and after a script without `BEGIN`. A block that wrote nothing is not found, and it loses nothing.
- What DbLive rolled back, for the message: everything the file ran, also the statements before `BEGIN`.
- [ ] The text of `DbLiveTransactionLeftOpenException`: `BEGIN TRANSACTION` is valid on PostgreSQL too, but "by itself or in a procedure it called" is not (a procedure cannot begin a transaction there). Pass that part from the data access, as `DbLiveTransactionEndedException` takes its example?
- [ ] After an error of the script the block is aborted, and Npgsql rolls it back on the return to the pool, as on SQL Server before `0096-178`. Roll it back at once too (`TryRollbackAsync`)? Its locks stay until then.

## Docs to update
- `docs/llms-full.txt` §0 rule 22, §10 ("A script must not leave a transaction open"), §16 (the row "A script or a SQL test that leaves a transaction open"), §17, §19 (the PostgreSQL limitation goes)
- `docs/deployment/migrations.md` ("No transaction left open")

## Done when
- [ ] Fixed, with integration tests on PostgreSQL (a migration with `None`, BeforeDeploy, a script whose statements before `BEGIN` are lost)
- [ ] Docs updated

## Notes
- Split out of `0096-178` by the maintainer on 2026-11-25.
