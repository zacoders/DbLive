---
created: 2026-11-27
---
# A lock timeout for migrations and breaking scripts, with retries

## Idea
A DDL statement takes an exclusive lock on its table: `ACCESS EXCLUSIVE` on PostgreSQL, `Sch-M` on SQL Server (`alter table`, `drop column`, an index build without `online`/`concurrently`). When a long query or an open transaction of the application holds a lock that conflicts, the DDL waits. While it waits, the requests that come after it queue behind it, reads too: on both engines a new request waits when an incompatible request waits before it. So a statement that itself takes milliseconds makes the table unavailable for as long as the slowest query on it runs. The deployment test runs on an empty database without other sessions, so it never shows this.

DbLive has nothing for it. `MigrationTimeout` (12 hours) and `Timeout` of the item settings limit the whole script, not the wait for a lock. Retries are for code files only, and only for the order of dependencies (Section 6 of `docs/llms-full.txt`). A rerun of the failed deployment is the only retry of a migration: with `TransactionWrapLevel: Migration` its transaction was rolled back, so a rerun is safe. The deployment lock (`sp_getapplock ... @LockTimeout = 0`, `pg_try_advisory_lock`) is a different thing.

Two steps:
1. A recipe in Section 18, maybe a hard rule: set the lock timeout at the top of a migration or breaking script that changes a busy table.
   - SQL Server: `set lock_timeout 5000;`. Each script gets a reset session (Section 10), so it applies to that script only.
   - PostgreSQL: `set lock_timeout = '5s';`. With `Migration` it lasts to the end of the migration's transaction. With `Deployment` it stays for the rest of the deployment, and `set local` does not help (Section 10): the script restores it at its end (`reset lock_timeout;`).
2. A setting: `LockTimeout` in `settings.json` and in the item settings files. DbLive sets it before the script and retries the script after a lock timeout (SQL Server `1222`, PostgreSQL `55P03`), N times with a pause. Only a script in a transaction of its own can be retried: the rollback undoes what it did.

Came up 2026-11-27, from a review of DbLive by an agent that was asked what would make it choose DbLive (chat with the maintainer).

## Open questions
- [ ] Recipe only, or the setting too?
- [ ] Default of the setting: none, as now, or a value such as 10 seconds? A value fails a migration that waited and succeeded before: a deployment that passed and now fails is a breaking change (`docs/versioning.md`).
- [ ] Which scripts: migrations, breaking scripts of migrations, breaking scripts of the Code folder? Code files (does `create or alter` of a procedure that is executing wait on SQL Server?), BeforeDeploy and AfterDeploy?
- [ ] SQL Server: error `1222` ends the statement, not the batch or the transaction, unless `XACT_ABORT` is on (Microsoft docs, not verified with DbLive). With `TransactionWrapLevel: None` the statements after it still run and commit. Does DbLive stop the script at the end of the batch? The recipe must say what to do then.
- [ ] Retries: count, pause, backoff. Retry `1205` (deadlock) too? Retry with `None` never, or when the item settings say the script is re-runnable?
- [ ] SQL Server `WAIT_AT_LOW_PRIORITY` (which statements, which versions?) as the recipe for index operations, instead of the lock timeout.
- [ ] How the timeout error reads: the message should say that the script waited for a lock and that a rerun is safe, or not.

## Docs to update
- `docs/llms-full.txt` Section 0 (if a hard rule), §9, §9.1, §10, §18.7 or a new recipe, Section 17 if a new exception
- `docs/deployment/zero-downtime.md`, `docs/project/configuration.md`

## See also
- `tasks/ideas/migration-breaking-change-check.md`: what a migration must not do to the previous application; this idea is about how long it blocks it
