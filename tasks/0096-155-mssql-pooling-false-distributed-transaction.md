---
type: docs
priority: low
created: 2026-11-24
---
# SQL Server: every deployment fails with `Pooling=False` in the connection string

## Problem
DbLive opens a new `SqlConnection` for each call of its data access, also several in one `TransactionScope`: the self-deployment (`DbLiveSelfDeployer.cs:34-67`: `DbLiveInstalledAsync`, `GetMinDbLiveLibVersionAsync`, the self-migrations, ...), a migration with its row, the deployment transaction. With pooling, the second connection of a scope gets the same session back from the pool of the transaction. With `Pooling=False` it is a new session, the transaction is promoted to a distributed one, and .NET fails it: `NotSupportedException: Implicit distributed transactions have not been enabled`. So with `...;Pooling=False` the first deployment fails in the self-deployment, before any script of the project. Verified: 0096-69 (Notes), and again by the review on 2026-11-24 with SqlClient 6.1.4 on SQL Server 2017 and 2025. PostgreSQL is not affected: with `Pooling=false` Npgsql keeps the enlisted session for the transaction (verified on PostgreSQL 13 the same day).

`docs/llms-full.txt` §11.1 (`SetDbConnection`, line 769) puts no requirement on the connection string, and §17 has no row for the error; §8.2 (line 512) names the error only for `ClearAllPools`.

Related: `Enlist=False` on either provider takes the connections out of the transactions of DbLive. The transaction check reads the ID of the transaction in autocommit mode then, and every script in a transaction fails with "The script ended the transaction that DbLive runs it in" (from the code, not run). An operator mistake: a line in the docs is enough.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:57` (`GetConnection`: the connection string as it is)

## Fix
- [ ] Force `Pooling=true` (and `Enlist=true`) in `GetConnection`, or document both keys? Forcing changes the pool key only when the user set them, so the recipe of 0096-81 (`ClearPool(new SqlConnection(connectionString))`) keeps working for everyone else.

## Docs to update
- `docs/llms-full.txt` §11.1 (`SetDbConnection`), §17 (a row for "Implicit distributed transactions have not been enabled")

## Done when
- [ ] Fixed or documented, as decided

## Notes
- Found by the code review of 2026-11-24.
