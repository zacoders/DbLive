---
type: docs
priority: medium
created: 2026-11-25
closed: 2026-11-26
---
# SQL Server: `ALTER DATABASE ... SET READ_COMMITTED_SNAPSHOT ON` in a migration waits for the sessions of DbLive

## Release note
None. The docs now say that on SQL Server an `ALTER DATABASE` option that needs the database to itself (`READ_COMMITTED_SNAPSHOT`, `ENABLE_BROKER`) takes `WITH ROLLBACK IMMEDIATE` in a deployment, and that it ends the deployment lock too; DbLive behaves as before.

## Problem
An `ALTER DATABASE` option that needs the database to itself (`READ_COMMITTED_SNAPSHOT`, `ENABLE_BROKER`) waits for every other session in the database. A deployment always has some there: the connection of the deployment lock, held for the whole deployment (§12.1), and the idle connections of the pool of DbLive. So the statement waits until the timeout of its script, `MigrationTimeout` (12 hours) by default.

Verified on 2026-11-25 on SQL Server 2025 with the packages packed from 8e0f0f4:
- `Migrations/002.migration.sql`: `alter database current set read_committed_snapshot on;`, with `002.migration.settings.json` `{ "TransactionWrapLevel": "None", "Timeout": "00:00:20" }`. The deployment fails after 20 seconds: `MigrationDeploymentException` with `ExecutionTimeoutException: Execution Timeout Expired. The script ran longer than its timeout of 00:00:20.`
- The same with `... with rollback immediate;`: the deployment succeeds in about 4 seconds, but the statement ends the session of the deployment lock too. The rest of the deployment runs without the lock, and its end logs the warning `The deployment lock was not released: its connection is broken. ...`, which then means nothing is wrong with the network.

The docs send `ALTER DATABASE` to a migration with `TransactionWrapLevel: None` (§9.3 line 709, §16 line 1076, `configuration.md:140`, `code.md:93`) and say nothing about this. Before 2.0 the idle connections of the pool were there as well; the session of the lock is new.

## Fix
Docs: an `ALTER DATABASE` option that needs the database to itself takes `with rollback immediate`; it ends the sessions of DbLive in the database, the deployment lock included, and the warning about the lock at the end of that deployment is expected.

- [x] Code: keep the lock, or let DbLive say something better than "its connection is broken" when the session of the lock was killed? The lock has to be in the database (`sp_getapplock` is local to it, §12.1). Decision of the maintainer, 2026-11-26: docs only in 2.0.0; the code that removes the problem is the idea `tasks/ideas/database-options-step.md`.

## Docs to update
- `docs/llms-full.txt` §9.3 (`TransactionWrapLevel` of the item settings), §12.1, §16 ("Statements that cannot run in a transaction")
- `docs/project/configuration.md` (the statements that cannot run in a transaction), `docs/deployment/code.md:93`
- `docs/llms-full.txt` §7 and `docs/project/structure.md:104`: they recommend BeforeDeploy for database options, which has the same problem
- `docs/deployment/index.md` ("A lock left by a lost deployment": the warning about the lock)

## Done when
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0); verified by experiment the same day.
- 2026-11-26, decision of the maintainer: a step for database options that removes the problem (a project folder run before the deployment lock) is written as an idea, `tasks/ideas/database-options-step.md`, and is not part of 2.0.0. This task documents the workaround.
- BeforeDeploy has the same problem, and §7 and `docs/project/structure.md` recommend it for database options. With `TransactionWrapLevel: Deployment` no `alter database` runs at all on SQL Server: BeforeDeploy and the migrations run in the deployment transaction, and an item `None` is ignored (follows from §5.3 and §10, not run).

## Result
Docs only, no code change.
- `docs/llms-full.txt`:
  - §16, "Statements that cannot run in a transaction": an option that needs the database to itself waits for the session of the lock and the idle connections of the pool until the timeout of its script, `with rollback immediate` runs it at once and ends the lock too (both verified, see Problem); `ALLOW_SNAPSHOT_ISOLATION` and `RECOVERY` do not need the database to themselves (`ItemTransactionSettingsTests` runs both in an item with `None`); with the project `TransactionWrapLevel: Deployment` none of these statements runs, on both providers.
  - §9.3: the `TransactionWrapLevel` row points to §16 for such an option.
  - §12.1: a script can end the session of the lock (`rollback immediate`, `kill`); the rest of that deployment runs without the lock, and the warning at its end is expected.
  - §7: in BeforeDeploy such an option is checked first, so that its `rollback immediate` runs only when the option changes, not on every deployment; with `Deployment` no `alter database` runs there.
- `docs/project/configuration.md` (item settings): the same as §16, with an example migration; the `Deployment` sentence.
- `docs/project/structure.md` (BeforeDeploy and AfterDeploy): the checked example.
- `docs/deployment/code.md`, `docs/deployment/index.md`: a sentence each, linking to the item settings.

Decisions: the docs do not name `with no_wait`: under DbLive the session of the lock is always in the database, so it probably always fails (not verified, not worth an experiment: a user who tries it gets an error at once, not a wait). A better message for the warning about the lock is not needed: the idea removes the case.
