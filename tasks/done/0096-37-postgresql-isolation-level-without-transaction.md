---
type: bug
priority: medium
created: 2026-11-03
closed: 2026-11-03
---
# PostgreSQL: the isolation level of a command without a transaction is ignored

## Release note
PostgreSQL: a script that runs without a transaction now runs at the configured isolation level. This covers code files, BeforeDeploy and AfterDeploy scripts, and migration, breaking and undo scripts with `"TransactionWrapLevel": "None"` (at the `TransactionIsolationLevel` of their item settings file). Before, DbLive sent `SET TRANSACTION ISOLATION LEVEL`, which PostgreSQL ignores outside a transaction block, so these scripts ran at the database default `default_transaction_isolation`. Nothing changes when `TransactionIsolationLevel` and the database default are both read committed (the defaults). If you set another `TransactionIsolationLevel`, these scripts now run at it; if your database has another `default_transaction_isolation`, the level of DbLive now wins for them.

## Problem
Outside a transaction, `PostgreSqlDA.ExecuteNonQueryAsync` sent `set transaction isolation level <level>;` as a separate command and then the script. PostgreSQL ignores `SET TRANSACTION` outside a transaction block: "If SET TRANSACTION is executed without a prior START TRANSACTION or BEGIN, it emits a warning and otherwise has no effect" (PostgreSQL documentation, SET TRANSACTION). So every command DbLive ran without a transaction on PostgreSQL ran at the server default (`default_transaction_isolation`, normally read committed):

- code files and breaking scripts of the Code folder,
- BeforeDeploy and AfterDeploy scripts,
- migration, breaking and undo scripts with `"TransactionWrapLevel": "None"`, also with a `TransactionIsolationLevel` in their item settings file.

`docs/llms-full.txt` said the level is "set on the connection when there is no transaction"; that held on SQL Server only (`MsSqlDA` sets it for the session). Until the fix, the docs described the PostgreSQL exception (§5.3, §9.2, §9.3).

Verified 2026-11-03 (see Notes). Impact is small while the level is `ReadCommitted`, the default of both.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: `SetTransactionIsolationLevelAsync`, called by `ExecuteNonQueryAsync` and `ExecuteQueryMultipleAsync` when there is no ambient transaction.

## Fix
- [x] `set session characteristics as transaction isolation level <level>;` on the connection (the session default for the implicit transaction of the command). Npgsql resets the session when the connection goes back to the pool (verified).
- [x] Test on PostgreSQL: a code file and a `None` migration see `current_setting('transaction_isolation')` = the configured level.
- [x] Is it breaking? Not marked `breaking: true` (agent's recommendation, 2026-11-03): with the defaults nothing changes, and a user who configured another level asked for it. The release note names who sees a change.

## Docs to update
- `docs/llms-full.txt` §5.3 step 3, §9.2 (`TransactionIsolationLevel`), §9.3 (item `TransactionIsolationLevel`): remove the PostgreSQL exception.
- `docs/project/configuration.md` (Transactions, Item settings).

## Done when
- [x] Fixed, with a test on PostgreSQL
- [x] Docs updated

## Notes
Found in the documentation review of #96 (2026-11-03).

Experiment 2026-11-03 (a temporary test in `DbLive.PostgreSQL.Tests`, `postgres:latest`, Npgsql 10.0.1, no ambient transaction), all verified:

- `ExecuteQueryMultipleAsync("select current_setting('transaction_isolation')", Serializable)` returns `read committed`.
- `ExecuteNonQueryAsync(<insert current_setting(...)>, RepeatableRead)` stores `read committed`.
- The server sends `WARNING: SET TRANSACTION can only be used in transaction blocks`; DbLive does not show it.
- `set session characteristics as transaction isolation level serializable;` as a separate command, then the script on the same connection: the script runs at `serializable`.
- The next open of the same physical connection (`Maximum Pool Size=1`, same `pg_backend_pid()`) is at `read committed` again: Npgsql resets the session on return to the pool (`No Reset On Close` is false by default).
- `create index concurrently` after `set session characteristics ...` on the same connection: works.
- `set transaction isolation level serializable; <script>` in one command: the script runs at `serializable` (an implicit transaction block), but `set transaction ...; create index concurrently ...` fails with `25001: CREATE INDEX CONCURRENTLY cannot run inside a transaction block`. Prepending the setting to the script breaks exactly the scripts that need `None`.

Options:

- **A. `set session characteristics` on the connection.** A one-line change in `SetTransactionIsolationLevelAsync`, works with scripts that cannot run in a transaction block. If a user sets `No Reset On Close=true`, the level stays on the pooled connection; DbLive sets it again before every script, so only the metadata queries of `PostgreSqlDA` would see it. They read and write DbLive tables in short statements, so the level does not change their result.
- B. Prepend the setting to the script: breaks `create index concurrently`, `vacuum` and similar.
- C. Wrap the script in `begin isolation level ...`: contradicts `"TransactionWrapLevel": "None"`.
- D. `Options=-c default_transaction_isolation=...` in the connection string: works, but one pool per level and more code than A.
- E. Do not fix: remove the useless command and keep the documented PostgreSQL exception.

Decision of the maintainer, 2026-11-03: option A.

Who sees a change after A: only users with a `TransactionIsolationLevel` other than `ReadCommitted`, or with a database whose `default_transaction_isolation` is not `read committed` (before, it won; after A, the configured level of DbLive wins).

## Result
- `PostgreSqlDA.SetTransactionIsolationLevelAsync` sends `set session characteristics as transaction isolation level <level>;` instead of `set transaction isolation level <level>;`. Inside an ambient transaction nothing changes: the level comes from the `TransactionScope`, as before.
- Tests in `src/DbLive.PostgreSQL.Tests/ItemTransactionSettingsTests.cs`:
  - `Scripts_without_a_transaction_run_at_the_configured_isolation_level`: project `RepeatableRead`, a `None` migration with item `Serializable`, a code file and an AfterDeploy script log `current_setting('transaction_isolation')`. Before the fix all three logged `read committed` (checked against the old code).
  - `Isolation_level_does_not_stay_on_the_pooled_connection`: with `Maximum Pool Size=1`, the next open of the same backend after a `Serializable` command is at `read committed`.
- Docs: `docs/llms-full.txt` §5.3, §9.2, §9.3 and `docs/project/configuration.md` no longer describe a PostgreSQL exception.
- Test runs: `DbLive.PostgreSQL.Tests` 41/41, `DbLive.Tests` 397/397.
