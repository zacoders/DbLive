---
type: bug
priority: medium
created: 2026-11-16
closed: 2026-11-16
breaking: true
---
# A code file with several batches is applied in part on SQL Server

## Release note
**On SQL Server a code file is applied as a whole.** A code file now runs in a transaction of its own: when one of its `GO` batches fails, the batches before it are rolled back. Before, they stayed applied, and the database had part of the file in its new version until a later deployment ran the file again. The files still deploy in parallel, and `CodeItemTimeout` stays the timeout of each batch. On PostgreSQL a file already ran as one implicit transaction, and with `TransactionWrapLevel: Deployment` nothing changes.

To upgrade: a statement that SQL Server does not allow in a transaction, such as `ALTER DATABASE` or full-text DDL, now fails in a code file with the error of SQL Server (`ALTER DATABASE statement not allowed within multi-statement transaction.`). Move it into a migration with `"TransactionWrapLevel": "None"` in its item settings file.

## Problem
Outside the deployment transaction (`TransactionWrapLevel` `Migration` or `None`, the default is `Migration`) a code file runs without a transaction. On SQL Server the file is split into its `GO` batches, and each batch commits on its own. When a later batch fails, the batches before it stay applied: the database has the first objects of the file in their new version and the others in the old one, until a deployment runs the whole file again.

The docs allow several objects in one file ("SQL Server: separate batches with `GO`"), and a logical object often takes more than one batch: a procedure and its `grant`, a type and the procedure that uses it.

On PostgreSQL the file is one command, which runs as one implicit transaction: it fails or succeeds as a whole. In the deployment transaction a failed file is rolled back to its savepoint (PostgreSQL) or fails the deployment, which rolls everything back.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:24-28` (`ExecuteCodeItemAsync` is `ExecuteNonQueryAsync`)
- `src/DbLive.MSSQL/MsSqlDA.cs:170-199` (`ExecuteNonQueryAsync`: SMO `ServerConnection.ExecuteNonQuery` runs the batches one by one)

## Fix
On SQL Server, outside the deployment transaction, run a code file in a transaction of its own: `begin transaction` on its connection, the batches, `commit`; after an error `rollback` when the transaction is still open (`XACT_ABORT ON` and some errors roll it back themselves). A transaction of T-SQL, not `TransactionScope`: there is no transaction timeout, `CodeItemTimeout` stays the command timeout of each batch, and the isolation level set on the connection applies. The files still run in parallel, each on its own connection. No setting.

A statement that SQL Server does not allow in a transaction (`ALTER DATABASE`, full-text DDL) then fails in a code file. Its place is a migration with `"TransactionWrapLevel": "None"` (Section 16 already says so for migrations).

## Docs to update
- `docs/llms-full.txt` §6 (code items and transactions), §6.1 (breaking scripts of the Code folder: "like a code file"), §9.2 (`TransactionIsolationLevel`), §10 (`Migration` level), §16 (script execution)
- `docs/deployment/code.md` ("Transactions and timeouts")
- `docs/deployment/breaking-changes.md` ("Execution")
- `docs/project/configuration.md` (`TransactionIsolationLevel`)

## Done when
- [x] A code file whose later batch fails leaves none of its batches applied on SQL Server, also with `XACT_ABORT ON`; integration test
- [x] A failed file leaves no open transaction on its pooled connection
- [x] The deployment transaction and PostgreSQL work as before
- [x] Docs updated

## Notes
- Came up 2026-11-16 in 0096-77: a failed file that left its first batch applied can unblock another file of its round, which the rule "a round that deploys no file stops the group" does not see. The maintainer decided to run each code file in its own transaction (2026-11-16).
- Breaking scripts of the Code folder keep running without a transaction of their own: they run on every breaking deployment and must be idempotent, so the next breaking deployment finishes a script that failed halfway.
- **Verified** on SQL Server with `CodeFileTransactionTests` run on the code before the fix: after a file failed in its second batch, the view of its first batch stayed, also with `set xact_abort on`; `alter database current set recovery simple` deployed as a code file. With the fix the view is not there, and the `alter database` fails with `ALTER DATABASE statement not allowed within multi-statement transaction.`
- `@@trancount` read in an `insert ... select` without a transaction is 2 (verified), so the test of the isolation level checks `sys.dm_tran_session_transactions.is_user_transaction` instead.
- `TransactionWrapLevel: None` does not turn the transaction of a code file off: code was never under that level (it has no item settings), and a file that is applied as a whole should not depend on a setting of the migrations. Decided while implementing, 2026-11-16.

## Result
- `MsSqlDA.ExecuteCodeItemAsync` outside the ambient transaction runs the file between `begin transaction` and `commit` on its connection (`ExecuteInOwnTransactionAsync`), with the isolation level set on the connection before. After an error it runs `if @@trancount > 0 rollback transaction`; an error of the rollback (a broken connection) is ignored, and the error of the file is thrown. `ExecuteNonQueryAsync` is unchanged; both go through `ExecuteScriptAsync`. In the deployment transaction and on PostgreSQL nothing changed.
- `breaking: true`: a code file with a statement that SQL Server does not allow in a transaction deployed before and fails now.
- Tests: `CodeFileTransactionTests` (SQL Server): a later batch that fails leaves nothing, with and without `XACT_ABORT ON`, and no session of the database keeps an open transaction; the file runs in a user transaction at the configured isolation level; `ALTER DATABASE` fails with the error of SQL Server. All four fail on the code before the fix. Unit 509, SQL Server 90 and PostgreSQL 60 tests pass.
- Docs: `docs/llms-full.txt` §6, §6.1, §9.2, §10 (`Migration` and `None`), §16; `docs/deployment/code.md`, `docs/deployment/breaking-changes.md`, `docs/project/configuration.md`; the summary of `IDbLiveDA.ExecuteCodeItemAsync`.
