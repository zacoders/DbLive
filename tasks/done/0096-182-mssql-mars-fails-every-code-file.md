---
type: bug
priority: high
created: 2026-11-25
closed: 2026-11-25
breaking: true
---
# SQL Server: `MultipleActiveResultSets=True` fails every code file

## Release note
SQL Server: `DeployAsync` and `RunAllTestsAsync` fail at their start with a `NotSupportedException` that names the setting when the connection string sets `MultipleActiveResultSets=True`, before they connect. Under MARS a transaction begun with T-SQL ends with its batch, and a code file now runs its `GO` batches in one such transaction: without the check every code file failed with "A transaction that was started in a MARS batch is still active at the end of the batch", after the migrations of the deployment were committed. DbLive 1.5 ran code files without a transaction and worked with MARS. Remove `MultipleActiveResultSets=True` from the connection string of DbLive and of its test fixture. The ASP.NET Core and EF Core templates put it into the connection string of the application, which can keep it.

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
- [x] Reject `MultipleActiveResultSets=True` in `CheckConnectionString` with a `NotSupportedException` that names the setting, as 0096-155 does for `Pooling=False` and `Enlist=False`? Or begin the transaction of a code file with `SqlConnection.BeginTransaction()` (a transaction of the client API is not batch-scoped), which needs `Transaction` set on every command of the file and of the transaction guard?
  - The first is one check and fails before anything connects; DbLive does not use MARS. The user removes the setting from the connection string of the deployer and of the test fixture (DbLive does not rewrite the connection string).
  - The second keeps MARS working for code files; a user script that spans `GO` batches with its own transaction still fails, as SQL Server says.

  Decided by the maintainer on 2026-11-25: the first, MARS is rejected.

A test on SQL Server either way: a code file deployed with `MultipleActiveResultSets=True`.

## Docs to update
- `docs/llms-full.txt` §6 (the transaction of a code file), §8.2 (the checks before the first test), §11.1 (`SetDbConnection`), §12 (the check before step 1), §17 (`NotSupportedException`)
- `docs/deployment/index.md` ("A deployer")
- `docs/unit-testing/index.md` (the checks of `RunAllTestsAsync`)

## Done when
- [x] Fixed, with a test on SQL Server
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-25 (the branch as release 2.0); verified by experiment the same day.
- Why it matters more than a failed deployment: with `TransactionWrapLevel: Migration` (the default) the migrations commit one by one before the code, so the database was left with the new schema and the old code, and a retry failed the same way. The deployment test of the user does not catch it when its fixture has another connection string (a container) than the production deployer (the one of the application).
- Not taken, the second option: besides `Transaction` on every command, the transaction guard was built for a session without MARS. After a `COMMIT` of the script, `IMPLICIT_TRANSACTIONS ON` of the guard opens a new transaction, which under MARS is batch-scoped too, so the script would fail with Msg 3997 instead of `DbLiveTransactionEndedException` (from the rule of SQL Server, not run). MARS would be a second configuration to test, for a setting that DbLive does not use.
- Not taken: setting `MultipleActiveResultSets=False` in the connection string of DbLive. It would silently override a setting of the user, against the decision of 0096-148 not to rewrite the connection string, and give DbLive another connection pool than the one `ClearPool` with the string of the user reaches (as for `Pooling` in 0096-155).
- `RunAllTestsAsync` rejects it too: the check is shared with the deployment, as for `Pooling` and `Enlist`. The SQL tests run in a `TransactionScope` and passed under MARS, but their fixture deploys the database with the same connection string first.
- Breaking: a project with code files that deployed under MARS with DbLive 1.5 fails now; it failed with 2.0 before this task as well.
- The release note of 0096-155 lists the settings it added and stays correct.

## Result
- `ConnectionStringNotSupported.MultipleActiveResultSets()`; `MsSqlDA.CheckConnectionString` rejects `MultipleActiveResultSets=True`. The check runs first in `DeployAsync` and `RunAllTestsAsync`, without connecting. The XML docs of `IDbLive.DeployAsync`, `IDbLiveTester.RunAllTestsAsync` and `IDbLiveDA.CheckConnectionString` name the setting.
- Test: `ConnectionStringSettingsTests.Deployment_with_mars_fails_before_it_connects` in `DbLive.MSSQL.Tests` (a project with a migration and a code file, deployed with MARS, fails with the message and leaves no table).
- Docs: `docs/llms-full.txt` §6, §8.2, §11.1, §12, a row in §17; `docs/deployment/index.md` ("A deployer"), `docs/unit-testing/index.md`.
