---
type: bug
priority: low
created: 2026-11-24
---
# SQL Server: every deployment fails with `Snapshot` and `TransactionWrapLevel: Deployment`

## Problem
With `"TransactionWrapLevel": "Deployment", "TransactionIsolationLevel": "Snapshot"` in `settings.json` every deployment fails, also of a project without a code file:

```
Microsoft.Data.SqlClient.SqlException : Transaction failed because this DDL statement is not allowed inside a snapshot isolation transaction.
   at DbLive.MSSQL.MsSqlDA.MarkMissingCodeAsRemovedAsync(...)
   at DbLive.Deployers.Code.CodeDeployer.MarkRemovedFilesAsync(...)
```

Verified on 2026-11-24 on SQL Server 2025 (a throwaway test while working on 0096-148): a database with `allow_snapshot_isolation on`, one migration `select 1;`, with and without two code files. With `TransactionWrapLevel: Migration` and `Snapshot` the same projects deploy twice.

The statement is DbLive's own: `PathsTableSql` (`MsSqlDA.cs`) creates `#paths` and then `create clustered index ix_paths on #paths`, and SQL Server does not allow `CREATE INDEX` in a snapshot transaction (Msg 3964), also on a temporary table. `MarkMissingCodeAsRemovedAsync` runs in every deployment, `MarkCodeAsApplyingAsync` and `MarkCodeAsVerifiedAsync` use the same SQL.

The docs (`docs/llms-full.txt` §9.2 `TransactionIsolationLevel`, §16 row "`Snapshot` isolation") say that `Snapshot` fails a script with `ALTER TABLE` or index DDL, not that it fails every deployment in the deployment transaction. No test deploys a project with `Snapshot` in `settings.json`.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs`: `PathsTableSql` and its three users
- `src/DbLive/Deployers/Code/CodeDeployer.cs:105` (`MarkRemovedFilesAsync`)

## Fix
- [ ] The index inside `create table` (`index ix_paths clustered ( value )`, SQL Server 2014 and later), a table variable with a primary key, or no table at all? The comment of `PathsTableSql` explains the index: the plan of the query after it does not depend on a guess of the number of xml nodes.
- [ ] A test that deploys a project with `Snapshot` in `settings.json`, with each `TransactionWrapLevel`

## Done when
- [ ] Fixed, with a test

## Notes
- Found while working on 0096-148, on 2026-11-24.
