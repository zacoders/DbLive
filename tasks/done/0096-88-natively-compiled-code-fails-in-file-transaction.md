---
type: bug
priority: high
created: 2026-11-17
closed: 2026-11-17
---
# SQL Server: code objects whose DDL is not allowed in a transaction cannot be deployed from `Code`

## Release note
None. It fixes a regression of 0096-83 that no release had: natively compiled modules and memory-optimized table types deploy from `Code` as in 1.5. The release note of 0096-83 names the exception.

## Problem
Since 0096-83 every SQL Server code file runs between `begin transaction` and `commit`, and nothing turns that off (decided in 0096-83 on 2026-11-16: no setting, and `TransactionWrapLevel: None` does not apply to code).

SQL Server does not allow the DDL of In-Memory OLTP objects in a user transaction: `create or alter procedure ... with native_compilation`, and a memory-optimized table type. A project that keeps such objects in `Code` deployed with 1.5. With 2.0 every deployment should fail with the error of SQL Server ("DDL statements ALTER, DROP and CREATE inside user transactions are not supported with memory optimized tables"), and the rounds of retries cannot fix it. Verified on SQL Server 2025 (see `## Notes`); a natively compiled module has its own text of the error.

0096-83 looked at `ALTER DATABASE` and full-text DDL, which are not code objects, and sends them to a migration with `"TransactionWrapLevel": "None"`. That does not fit a code object: a migration runs once, so every change of the procedure would be a new migration. What is new since that decision: a re-runnable code object with this restriction was not considered there.

With `TransactionWrapLevel: Deployment` such objects never deployed from `Code`, in 1.5 too: the file runs inside the deployment transaction.

Found by the code review of 2026-11-17 (finding 2).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:28-32` (`ExecuteCodeItemAsync`: `ownTransaction` whenever there is no ambient transaction), `:320-333` (`ExecuteInOwnTransactionAsync`)
- `docs/deployment/code.md:90`, `docs/llms-full.txt:335` and `:1031` (the advice to use a migration with `TransactionWrapLevel: None`)
- `tasks/done/0096-83-code-file-partly-applied-on-sql-server.md:13` (the "To upgrade" part of its release note, same release)

## Fix
Reproduce first. Then the maintainer decides:

- [x] Which way? A file with the word `native_compilation` or `memory_optimized` runs without a transaction of its own (2026-11-17, see `## Notes`).
  1. **Docs only.** Name the place for such objects. AfterDeploy runs without a transaction of its own (outside `Deployment`), but on every deployment, so the procedure is compiled again each time. A migration with `None` is not re-runnable.
  2. **A way to run a code file without its transaction.** Code has no item settings files, so it needs a new form: a project setting, or a mark of the file. The file is then applied batch by batch, as in 1.5.
- [x] Before 2.0 or after it? Before: it needs no setting, so it is a fix, and 2.0 ships without the regression.

## Docs to update
- `docs/llms-full.txt` §6 (code items and transactions), §16 ("Statements that cannot run in a transaction"), §19 when it stays a limitation
- `docs/deployment/code.md` ("Transactions and timeouts")
- Release note of 0096-83 ("To upgrade"), in the same release

## Done when
- [x] Reproduced on SQL Server, or ruled out: a natively compiled procedure and a memory-optimized table type as code files
- [x] The maintainer decided, and the decision is under `## Notes` with its date
- [x] Implemented with an integration test, or documented as a limitation with the way around it
- [x] Docs and the release note of 0096-83 updated

## Notes
- **Verified** on SQL Server 2025 (17.0.1155), in a database with a `MEMORY_OPTIMIZED_DATA` filegroup, with `sqlcmd`, not through DbLive: after `begin transaction`, `create or alter procedure ... with native_compilation` fails with Msg 12331 `DDL statements ALTER, DROP and CREATE inside user transactions are not supported with natively compiled modules.`, for a new procedure and for an existing one; `create type ... as table ... with (memory_optimized = on)` fails with Msg 12331 `... with memory optimized tables.` Without a transaction both are created. The text of the error in `## Problem` is the one of the type; the procedure has its own.
- **Verified**: after Msg 12331 `@@trancount` is 0: SQL Server rolls back the whole transaction, with the batches of the file before the failed one (a `create table` in an earlier batch is gone). So no retry fixes it. After Msg 226 (`ALTER DATABASE`) the transaction stays open, and DbLive rolls it back.
- Why 0096-83 added the transaction, weighed again on 2026-11-17: a file with several `GO` batches was applied in part when a later batch failed; found by analysis in 0096-77, not reported by users. The harm is small: only files with several batches, after a deployment that failed anyway and left migrations and other code files committed, until the next deployment runs the file again. It matters for `drop` and `create` in separate batches: a failed `create` leaves no object at all, and the old application breaks. Reverting 0096-83 was considered (2.0 is not released yet) and not taken.
- Options considered:
  1. No transaction for a file of one batch: one batch is not one statement (`if type_id(...) is null create type ...; grant ...;` would be applied in part again), and a native procedure with `go` and a `grant` would fail again, for a reason the user cannot see.
  2. A settings file for a folder of `Code` with `TransactionWrapLevel: None`: a new concept of the project format (name, inheritance into nested folders, checks, exceptions), a feature after 2.0, and projects of 1.5 still fail on the upgrade until they move their files.
  3. Run the file again without a transaction after Msg 12331: no new format, but the file fails first and runs twice, and the log shows an error.
  4. Decide from the text before running: a file with the word `native_compilation` or `memory_optimized` runs without a transaction of its own, as in 1.5. Flyway does the same: it finds statements that cannot run in a transaction when it parses a migration and runs it without one (`executeInTransaction` overrides), also for memory-optimized tables. A word in a comment or a string runs the file without the transaction (atomicity lost, as in 1.5); DDL in dynamic SQL or a `drop` alone still fails with Msg 12331. Neither gives a wrong result silently.
- **Decision of the maintainer (2026-11-17): option 4.** No setting, so the decision of 0096-83 ("no setting") stands. Words are matched whole, so `is_memory_optimized`, `uses_native_compilation` and `memory_optimized_data` do not count. With `TransactionWrapLevel: Deployment` it stays as in 1.5: the file runs in the deployment transaction and fails.

## Result
- `IDbLiveDA.RunsCodeItemWithoutTransaction` (internal): on SQL Server true when the text of the file has the whole word `native_compilation` or `memory_optimized`, ignoring case (`MsSqlDA`, a generated regex); on PostgreSQL false. `MsSqlDA.ExecuteCodeItemAsync` runs such a file outside the deployment transaction without a transaction of its own, its batches one by one, as 1.5 did. In the deployment transaction nothing changed.
- `CodeItemDeployer` logs `Code file {filePath} runs without a transaction of its own: ...` for such a file, outside the deployment transaction. The adapters log nothing and do not know the path, so the deployer asks the adapter.
- Decision: option 4 of `## Notes` (2026-11-17). No setting; `breaking` is not set, because 1.5 ran these files the same way.
- Tests: `MsSqlDAInMemoryOltpTests` (the words, also in a comment; `is_memory_optimized`, `uses_native_compilation`, `memory_optimized_data`, `memory_optimized_elevate_to_snapshot` do not count); `CodeItemDeployerTests` (the log line, none in the deployment transaction); `CodeFileTransactionTests.Code_file_with_in_memory_oltp_ddl_deploys_without_a_transaction` (a migration with `None` adds the filegroup, a memory-optimized table type and a natively compiled procedure that uses it, then a change of the procedure). **Verified**: the integration test fails with Msg 12331 on the code without the fix. Unit 538, SQL Server 144 tests pass; PostgreSQL 63 of 64: `SqlTestResultSetTests.Rows_test_compares_NaN_and_infinity` fails on this machine on the commit before this change too.
- Docs: `docs/llms-full.txt` §6 (the exception, the words, the log, what still fails), §16 (script execution, statements that cannot run in a transaction); `docs/deployment/code.md` ("Transactions and timeouts"); the release note of 0096-83; the summary of `IDbLiveDA.ExecuteCodeItemAsync`. §19 is unchanged: what still fails (`TransactionWrapLevel: Deployment`, a `drop` alone, dynamic SQL) is in §6, and it failed in 1.5 too.
