---
type: bug
priority: high
created: 2026-11-17
---
# SQL Server: code objects whose DDL is not allowed in a transaction cannot be deployed from `Code`

## Problem
Since 0096-83 every SQL Server code file runs between `begin transaction` and `commit`, and nothing turns that off (decided in 0096-83 on 2026-11-16: no setting, and `TransactionWrapLevel: None` does not apply to code).

SQL Server does not allow the DDL of In-Memory OLTP objects in a user transaction: `create or alter procedure ... with native_compilation`, and a memory-optimized table type. A project that keeps such objects in `Code` deployed with 1.5. With 2.0 every deployment should fail with the error of SQL Server ("DDL statements ALTER, DROP and CREATE inside user transactions are not supported with memory optimized tables"), and the rounds of retries cannot fix it. From the documented restriction of SQL Server, not run.

0096-83 looked at `ALTER DATABASE` and full-text DDL, which are not code objects, and sends them to a migration with `"TransactionWrapLevel": "None"`. That does not fit a code object: a migration runs once, so every change of the procedure would be a new migration. What is new since that decision: a re-runnable code object with this restriction was not considered there.

With `TransactionWrapLevel: Deployment` such objects never deployed from `Code`, in 1.5 too: the file runs inside the deployment transaction.

Found by the code review of 2026-11-17 (finding 2).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:28-32` (`ExecuteCodeItemAsync`: `ownTransaction` whenever there is no ambient transaction), `:320-333` (`ExecuteInOwnTransactionAsync`)
- `docs/deployment/code.md:90`, `docs/llms-full.txt:335` and `:1031` (the advice to use a migration with `TransactionWrapLevel: None`)
- `tasks/done/0096-83-code-file-partly-applied-on-sql-server.md:13` (the "To upgrade" part of its release note, same release)

## Fix
Reproduce first. Then the maintainer decides:

- [ ] Which way?
  1. **Docs only.** Name the place for such objects. AfterDeploy runs without a transaction of its own (outside `Deployment`), but on every deployment, so the procedure is compiled again each time. A migration with `None` is not re-runnable.
  2. **A way to run a code file without its transaction.** Code has no item settings files, so it needs a new form: a project setting, or a mark of the file. The file is then applied batch by batch, as in 1.5.
- [ ] Before 2.0 or after it? After the release an opt-out is a feature of a minor version, and 2.0 ships with the regression and a documented way around it.

## Docs to update
- `docs/llms-full.txt` §6 (code items and transactions), §16 ("Statements that cannot run in a transaction"), §19 when it stays a limitation
- `docs/deployment/code.md` ("Transactions and timeouts")
- Release note of 0096-83 ("To upgrade"), in the same release

## Done when
- [ ] Reproduced on SQL Server, or ruled out: a natively compiled procedure and a memory-optimized table type as code files
- [ ] The maintainer decided, and the decision is under `## Notes` with its date
- [ ] Implemented with an integration test, or documented as a limitation with the way around it
- [ ] Docs and the release note of 0096-83 updated
