---
type: bug
priority: medium
created: 2026-11-03
---
# PostgreSQL: the isolation level of a command without a transaction is ignored

## Problem
Outside a transaction, `PostgreSqlDA.ExecuteNonQueryAsync` sends `set transaction isolation level <level>;` as a separate command and then the script. PostgreSQL ignores `SET TRANSACTION` outside a transaction block: "If SET TRANSACTION is executed without a prior START TRANSACTION or BEGIN, it emits a warning and otherwise has no effect" (PostgreSQL documentation, SET TRANSACTION). So every command DbLive runs without a transaction on PostgreSQL runs at the server default (`default_transaction_isolation`, normally read committed):

- code files and breaking scripts of the Code folder,
- BeforeDeploy and AfterDeploy scripts,
- migration, breaking and undo scripts with `"TransactionWrapLevel": "None"`, also with a `TransactionIsolationLevel` in their item settings file.

`docs/llms-full.txt` said the level is "set on the connection when there is no transaction"; that holds on SQL Server only (`MsSqlDA` sets it for the session). The docs now describe the current behavior (§5.3, §9.2, §9.3).

Not verified by an experiment; based on the PostgreSQL documentation and the code. Impact is small while the level is `ReadCommitted`, the default of both.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: `ExecuteNonQueryAsync`, `SetTransactionIsolationLevelAsync`, and `ExecuteQueryMultipleAsync` if it does the same.

## Fix
- [ ] `set session characteristics as transaction isolation level <level>;` on the connection (the session default for the implicit transaction of the command), or prepend it to the command. The connection goes back to the pool afterwards: check that Npgsql resets the session (`DISCARD ALL`) or reset it.
- [ ] Test on PostgreSQL: a code file and a `None` migration see `current_setting('transaction_isolation')` = the configured level.
- [ ] Is it breaking? A script that relied on read committed while `Serializable` is configured could start to fail with serialization errors. Release note, maybe `breaking: true`?

## Docs to update
- `docs/llms-full.txt` §5.3 step 3, §9.2 (`TransactionIsolationLevel`), §9.3 (item `TransactionIsolationLevel`): remove the PostgreSQL exception.
- `docs/project/configuration.md` (Transactions, Item settings).

## Done when
- [ ] Fixed, with a test on PostgreSQL
- [ ] Docs updated

## Notes
Found in the documentation review of #96 (2026-11-03).
