---
type: bug
priority: high
created: 2026-11-25
---
# SQL Server: `MultipleActiveResultSets=True` fails every code file

## Problem
With `MultipleActiveResultSets=True` in the connection string every code file fails on SQL Server, in every deployment that runs one, with the default `TransactionWrapLevel`:

```
CodeDeploymentAggregateException: Code deployment failed. (Deploy code file error. File path: Code/dbo.GetT.sql
  CodeDeploymentException: Deploy code file error. File path: Code/dbo.GetT.sql
    DbLiveSqlException: A transaction that was started in a MARS batch is still active at the end of the batch. The transaction is rolled back.
```

Verified on 2026-11-25 on SQL Server 2025 (`mcr.microsoft.com/mssql/server:2025-latest`) with the packages packed from 8e0f0f4: a project with `Migrations/001.migration.sql` (`create table`) and `Code/dbo.GetT.sql` (`create or alter procedure`), deployed with `DeployParameters.Default with { CreateDbIfNotExists = true }`. Without MARS the same project deploys. With MARS the migration is applied, the code file fails after 1 attempt.

Cause: under MARS a transaction begun with T-SQL (`BEGIN TRANSACTION`, or implicit) is batch-scoped. SQL Server rolls it back with Msg 3997 when it is still active at the end of its batch. The code file of 2.0 runs in a transaction of its own that DbLive begins with a batch of its own, `begin transaction;`, then sends the guard and the `GO` batches of the file, then `commit transaction;`. The transaction dies at the end of the first batch. Transactions of `TransactionScope` are begun through the client API and are not batch-scoped: migrations, the self-deployment, `Deployment` and the SQL tests are not affected (the migration of the experiment passed).

DbLive 1.5 ran code files without a transaction, so this is new in 2.0. `MultipleActiveResultSets=True` is in the connection strings of the ASP.NET Core and EF Core templates, so a deployer that takes the connection string of the application meets it on its first deployment.

A user script meets the same rule: a migration with `TransactionWrapLevel: None` that has `begin transaction` in one `GO` batch and `commit` in the next fails with Msg 3997 under MARS (verified in the same run). That is SQL Server behavior, not DbLive's.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:332-345` (`ExecuteInOwnTransactionAsync`): `begin transaction;` as a batch of its own.
- `src/DbLive.MSSQL/MsSqlDA.cs:58-64` (`CheckConnectionString`): rejects `Pooling=False` and `Enlist=False` only.
- `src/DbLive/Adapter/ConnectionStringNotSupported.cs`.

## Fix
- [ ] Reject `MultipleActiveResultSets=True` in `CheckConnectionString` with a `NotSupportedException` that names the setting, as 0096-155 does for `Pooling=False` and `Enlist=False`? Or begin the transaction of a code file with `SqlConnection.BeginTransaction()` (a transaction of the client API is not batch-scoped), which needs `Transaction` set on every command of the file and of the transaction guard?
  - The first is one check and fails before anything connects; DbLive does not use MARS. The user removes the setting from the connection string of the deployer and of the test fixture (DbLive does not rewrite the connection string).
  - The second keeps MARS working for code files; a user script that spans `GO` batches with its own transaction still fails, as SQL Server says.

A test on SQL Server either way: a code file deployed with `MultipleActiveResultSets=True`.

## Docs to update
- `docs/llms-full.txt` §11.1 (`SetDbConnection`), §12 (the check before step 1), §17 (`NotSupportedException`)
- `docs/project/configuration.md` or the page that lists the unsupported connection string settings

## Done when
- [ ] Fixed, with a test on SQL Server
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0); verified by experiment the same day.
