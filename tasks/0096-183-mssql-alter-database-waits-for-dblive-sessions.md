---
type: docs
priority: medium
created: 2026-11-25
---
# SQL Server: `ALTER DATABASE ... SET READ_COMMITTED_SNAPSHOT ON` in a migration waits for the sessions of DbLive

## Problem
An `ALTER DATABASE` option that needs the database to itself (`READ_COMMITTED_SNAPSHOT`, `ENABLE_BROKER`) waits for every other session in the database. A deployment always has some there: the connection of the deployment lock, held for the whole deployment (§12.1), and the idle connections of the pool of DbLive. So the statement waits until the timeout of its script, `MigrationTimeout` (12 hours) by default.

Verified on 2026-11-25 on SQL Server 2025 with the packages packed from 8e0f0f4:
- `Migrations/002.migration.sql`: `alter database current set read_committed_snapshot on;`, with `002.migration.settings.json` `{ "TransactionWrapLevel": "None", "Timeout": "00:00:20" }`. The deployment fails after 20 seconds: `MigrationDeploymentException` with `ExecutionTimeoutException: Execution Timeout Expired. The script ran longer than its timeout of 00:00:20.`
- The same with `... with rollback immediate;`: the deployment succeeds in about 4 seconds, but the statement ends the session of the deployment lock too. The rest of the deployment runs without the lock, and its end logs the warning `The deployment lock was not released: its connection is broken. ...`, which then means nothing is wrong with the network.

The docs send `ALTER DATABASE` to a migration with `TransactionWrapLevel: None` (§9.3 line 709, §16 line 1076, `configuration.md:140`, `code.md:93`) and say nothing about this. Before 2.0 the idle connections of the pool were there as well; the session of the lock is new.

## Fix
Docs: an `ALTER DATABASE` option that needs the database to itself takes `with rollback immediate` (or `with no_wait` to fail at once); it ends the sessions of DbLive in the database, the deployment lock included, and the warning about the lock at the end of that deployment is expected.

- [ ] Code: keep the lock, or let DbLive say something better than "its connection is broken" when the session of the lock was killed? The lock has to be in the database (`sp_getapplock` is local to it, §12.1).

## Docs to update
- `docs/llms-full.txt` §9.3 (`TransactionWrapLevel` of the item settings), §12.1, §16 ("Statements that cannot run in a transaction")
- `docs/project/configuration.md` (the statements that cannot run in a transaction), `docs/deployment/code.md:93`

## Done when
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0); verified by experiment the same day.
