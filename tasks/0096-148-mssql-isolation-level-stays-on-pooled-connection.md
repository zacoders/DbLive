---
type: bug
priority: medium
created: 2026-11-24
---
# SQL Server: the isolation level of DbLive stays on the pooled connections

## Problem
SQL Server does not reset the transaction isolation level of a session when a pooled connection is reused (`sp_reset_connection` resets `IMPLICIT_TRANSACTIONS`, `XACT_ABORT`, `NOCOUNT` and temporary tables, not the isolation level). Verified on 2026-11-24 on SQL Server 2017 and 2025 with SqlClient 6.1.4, in a program of its own (not through DbLive):

- In one `ReadCommitted` `TransactionScope`, script 1 ran `set transaction isolation level serializable; set xact_abort on; set nocount on; create table #t (...)`. Script 2, on a new `SqlConnection` in the same scope, saw `xact_abort` 0, `nocount` 0 and no `#t`, but the isolation level 4 (serializable).
- After a `Serializable` `TransactionScope`, and after a plain `set transaction isolation level serializable` and `Close()`, the next connection from the pool is at level 4.

What follows for DbLive (from the code, not run through DbLive):

1. The connections of DbLive go back to the pool of the user's connection string at the level DbLive left: `Serializable` after the self-deployment (`DbLiveSelfDeployer.cs:14`), after SQL tests (`TestsTransactionIsolationLevel`, `Serializable` by default), and after a command without a transaction at `TransactionIsolationLevel` (`MsSqlDA.SetTransactionIsolationLevelAsync`, `MsSqlDA.cs:420-424`). DbLive passes the connection string to SqlClient as it is (`MsSqlDA.cs:57`), so an application in the same process with the same connection string gets these connections: a deployment at the startup of a single-instance application (rule 20 allows it), or `RunAllTestsAsync` next to C# tests. Its autocommit statements and its transactions without a level (EF Core `SaveChanges`) then run at `SERIALIZABLE`: more blocking, more deadlocks. DbLive itself is not harmed: its scripts and scopes set their level.
2. `docs/llms-full.txt` §10 (line 731), §16 (line 1070), §8.2 (line 464), `docs/project/configuration.md:108` and `docs/unit-testing/index.md:121` say that on SQL Server each script gets a reset session, so a `SET` option does not reach the next script, nor the test after `setup.test.sql`. Not for the isolation level: with `TransactionWrapLevel: Deployment` a `set transaction isolation level` of one script applies to the scripts after it, and one of `setup.test.sql` to the test.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:57` (`GetConnection`), `:420-424` (`SetTransactionIsolationLevelAsync`)
- `src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs:14`
- `src/DbLive/Common/TransactionScopeManager.cs`

## Fix
- [ ] Fix it in the code, or document it? Options: (a) a pool of DbLive's own: `GetConnection` changes a key of the pool, for example `Application Name`, so the connections of DbLive never reach the application. It changes the recipe "A test database without xUnit" (§8.2), which clears the pool of DbLive with `SqlConnection.ClearPool(new SqlConnection(connectionString))`, and 0096-81 relies on DbLive passing the connection string as it is. (b) Set `read committed` on the connection before it goes back to the pool: it does not cover a connection enlisted in a `TransactionScope`, whose level stays after the scope. (c) Document it: an application that deploys or runs the SQL tests in its own process uses another connection string (another `Application Name`).
- [ ] Docs: the isolation level is the exception to "each script gets a reset session" (needed with every option).

## Docs to update
- `docs/llms-full.txt` §8.2, §10 ("Session state between scripts"), §16 (row "Session state between the scripts of one transaction")
- `docs/project/configuration.md` (`Deployment`), `docs/unit-testing/index.md` (setup file)

## Done when
- [ ] Verified through DbLive: after `DeployAsync`, a `SqlConnection` with the same connection string reads `transaction_isolation_level` of its session
- [ ] Fixed or documented, as decided
- [ ] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- PostgreSQL is not affected: Npgsql resets the session (`discard all`) when the connection goes back to the pool (verified in 0096-37).
