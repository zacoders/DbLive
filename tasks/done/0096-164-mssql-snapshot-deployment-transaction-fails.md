---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# SQL Server: every deployment fails with `Snapshot` and `TransactionWrapLevel: Deployment`

## Release note
None. DbLive 1.5 did not fail this way: the SQL of DbLive that failed came in 2.0.

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
- [x] The index inside `create table` (`index ix_paths clustered ( value )`, SQL Server 2014 and later), a table variable with a primary key, or no table at all? The comment of `PathsTableSql` explains the index: the plan of the query after it does not depend on a guess of the number of xml nodes. Decision of the maintainer on 2026-11-24: the index inside `create table`.
- [x] A test that deploys a project with `Snapshot` in `settings.json`, with each `TransactionWrapLevel`

## Docs to update
None. `docs/llms-full.txt` §9.2 and §16 and `docs/project/configuration.md` say that `Snapshot` fails a script in a transaction with `ALTER TABLE` or index DDL; after the fix that is all it fails.

## Done when
- [x] Fixed, with a test

## Notes
- Found while working on 0096-148, on 2026-11-24.
- Other ways that were weighed: a heap without an index (works, the temporary table alone gives the plan the number of rows); a table variable (no statistics, and no primary key: paths that differ only by accent or width are equal for such a collation); the xml in the query without a table (the guess of the number of nodes comes back); a validation error for `Snapshot` with `Deployment` (takes away a combination that works for a project without table or index DDL); the commands on another connection outside the deployment transaction (the marks would not roll back with the deployment).
- Verified on 2026-11-24 on SQL Server 2025: `SnapshotIsolationTests` failed before the fix with `Deployment` (Msg 3964 in `MarkMissingCodeAsRemovedAsync`) and passed with `None` and `Migration`; after the fix it passes with all three. An index inside `create table` is allowed in a snapshot transaction.

## Result
- `PathsTableSql` in `MsSqlDA.cs` declares the clustered index `ix_paths` inside `create table #paths` instead of a `create clustered index` after the insert. The index stays non-unique; the comment of `PathsTableSql` says why the index is inside `create table`.
- `SnapshotIsolationTests.Project_with_snapshot_isolation_deploys`: with `Snapshot` in `settings.json` and each `TransactionWrapLevel` (`None`, `Migration`, `Deployment`), two deployments, the second with an unchanged, a changed and a removed code file, so each of the three commands with `#paths` runs in the snapshot transaction.
- `SessionStateTests`: the theory on the isolation level in the deployment transaction has a `Snapshot` case now, a deployment instead of the data-access test that 0096-148 wrote because of this bug; that test is removed.
