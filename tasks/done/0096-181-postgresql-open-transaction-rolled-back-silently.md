---
type: bug
priority: medium
created: 2026-11-25
closed: 2026-11-25
breaking: true
---
# PostgreSQL: a script without a transaction that leaves one open is rolled back without an error

## Release note
**PostgreSQL: a script without a transaction that leaves one open now fails.** A script that runs without a transaction of DbLive (`TransactionWrapLevel: None`; BeforeDeploy, AfterDeploy and the breaking scripts of the Code folder unless the project uses `Deployment`) and ran `BEGIN` without `COMMIT` raised no error: Npgsql rolled the transaction block back when the connection went back to the pool. The deployment succeeded, the migration or breaking script was recorded as applied, and nothing of the file stayed, also not the statements before `BEGIN`. DbLive now checks the session after such a script and fails it with its file and `The script left a transaction open: it ran BEGIN without COMMIT. ...`. It rolls the block back at once: only what the file committed before `BEGIN` stays. A block that wrote nothing is not found, and it loses nothing. A script without a transaction that fails with an error is rolled back at once too.

To upgrade:
- End every `BEGIN` with `COMMIT` in the same file, or remove it: the file is one command, which runs in one implicit transaction. Such a script deployed before and lost what it did; now it fails every time.

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
- What DbLive rolled back, for the message: everything the file ran after its last `COMMIT`, also the statements before `BEGIN`; without a `COMMIT` in the file, all of it.
- [x] The text of `DbLiveTransactionLeftOpenException`: `BEGIN TRANSACTION` is valid on PostgreSQL too, but "by itself or in a procedure it called" is not (a procedure cannot begin a transaction there). Pass that part from the data access, as `DbLiveTransactionEndedException` takes its example? Yes: the exception takes the cause from the data access (`LeftOpenCause` of each), decision of the maintainer (2026-11-25).
- [x] After an error of the script the block is aborted, and Npgsql rolls it back on the return to the pool, as on SQL Server before `0096-178`. Roll it back at once too (`TryRollbackAsync`)? Its locks stay until then. Yes, decision of the maintainer (2026-11-25). The experiment then showed that Npgsql rolls it back at once when the connection goes back to the pool, so no locks stay (Notes); the rollback of DbLive is kept and does not rely on it.

## Docs to update
- `docs/llms-full.txt` §0 rule 22, §10 ("A script must not leave a transaction open"), §13.3, §16 (the row "A script or a SQL test that leaves a transaction open"), §17, §19 (the PostgreSQL limitation goes)
- `docs/deployment/migrations.md` ("No transaction left open")

## Done when
- [x] Fixed, with integration tests on PostgreSQL (a migration with `None`, BeforeDeploy, a script whose statements before `BEGIN` are lost)
- [x] Docs updated

## Notes
- Split out of `0096-178` by the maintainer on 2026-11-25.
- 2026-11-25, the fix: `ExecuteWithoutTransactionAsync` of `PostgreSqlDA` runs the script, then `select pg_current_xact_id_if_assigned() is not null` as a command of its own; true → `rollback;` and `DbLiveTransactionLeftOpenException(LeftOpenCause, LeftOpenWithoutTransaction)`. After an error of the script `TryRollbackAsync`, and the error of the script is thrown. Not a statement added to the script: a script without a transaction is often one that cannot run in a transaction block (`create index concurrently`), and the statements of one command run in one implicit transaction.
- `DbLiveTransactionLeftOpenException` takes the cause: `it ran BEGIN TRANSACTION without COMMIT, by itself or in a procedure it called` on SQL Server, `it ran BEGIN without COMMIT` on PostgreSQL. Its second sentence is neutral now: "It would be rolled back unnoticed with the connection." The SQL Server message of `0096-178` is in the same release, so no user saw the old text.
- Verified with the tests: `insert 1; commit; begin; insert 2;` in a migration with `None` keeps row `1`: the `COMMIT` of the script ends the implicit transaction, and only what follows is in the block.
- Verified with a temporary test (not kept): `begin; insert ...; select 1 / 0;` on a pooled Npgsql connection. With the connection kept open, `pg_stat_activity` of the database shows one session `idle in transaction (aborted)`; after `CloseAsync` none. Npgsql 10.0.1 rolls an open block back when the connection goes back to the pool, at once, unlike SQL Server, which rolls back when the session is reused. So the error case of `Script_without_a_transaction_that_leaves_one_open_leaves_no_session_in_a_transaction` passes also without the rollback of DbLive (verified).
- After an error of a script that left no block open, the `rollback` of `TryRollbackAsync` is a `WARNING: there is no transaction in progress` in the log of the server (documented behavior of PostgreSQL), once per failed script without a transaction.
- Tests (verified): `src/DbLive.PostgreSQL.Tests/ScriptLeavesTransactionOpenTests.cs`, 11 cases. With the check turned off, 7 fail; the 4 that pass are the 3 scripts that leave no block open and the error case above.

## Result
- PostgreSQL: a script without a transaction of DbLive (`None`, BeforeDeploy, AfterDeploy, the breaking scripts of the Code folder without `Deployment`) that leaves a transaction block that wrote open fails with `DbLiveTransactionLeftOpenException`, and DbLive rolls the block back. After an error of such a script DbLive rolls back too.
- `DbLiveTransactionLeftOpenException` takes the cause from the data access; the text of SQL Server is unchanged but for the neutral second sentence.
- Tests: `ScriptLeavesTransactionOpenTests` on PostgreSQL (11 cases: a migration with `None`, with the statements before `BEGIN` lost and with a `COMMIT` before `BEGIN`; BeforeDeploy and AfterDeploy; a breaking script of the Code folder; no session left in a transaction, also after an error; scripts that leave none open).
- Docs: `docs/llms-full.txt` §0 rule 22, §10, §13.3, §16, §17, §19 (the limitation is gone); `docs/deployment/migrations.md`.
