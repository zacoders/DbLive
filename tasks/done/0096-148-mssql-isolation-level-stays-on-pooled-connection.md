---
type: bug
priority: medium
created: 2026-11-24
closed: 2026-11-24
---
# SQL Server: the isolation level stays on the pooled connections

## Release note
SQL Server: a `set transaction isolation level` in a script no longer changes the level of the scripts after it in the same transaction: with `TransactionWrapLevel: Deployment` the migrations and code files after it, and the SQL test after its `setup.test.sql`. SQL Server keeps the isolation level of a session when a pooled connection is reused, also for the next connection of one transaction, so DbLive now sets the level of the transaction before each script. A script that relied on the level a script before it set sets the level itself.

If your application deploys its database at startup, or runs the SQL tests in the process of C# tests, with the same connection string as DbLive, give DbLive another `Application Name` (`...;Application Name=MyApp.DbLive`). With the same connection string the application can get a connection of DbLive from the pool at the level of its last transaction, `Serializable` after the SQL tests by default. The docs now say so.

## Problem
SQL Server does not reset the transaction isolation level of a session when a pooled connection is reused (`sp_reset_connection` resets `IMPLICIT_TRANSACTIONS`, `XACT_ABORT`, `NOCOUNT` and temporary tables, not the isolation level). Verified on 2026-11-24 on SQL Server 2017 and 2025 with SqlClient 6.1.4, in a program of its own (not through DbLive):

- In one `ReadCommitted` `TransactionScope`, script 1 ran `set transaction isolation level serializable; set xact_abort on; set nocount on; create table #t (...)`. Script 2, on a new `SqlConnection` in the same scope, saw `xact_abort` 0, `nocount` 0 and no `#t`, but the isolation level 4 (serializable).
- After a `Serializable` `TransactionScope`, and after a plain `set transaction isolation level serializable` and `Close()`, the next connection from the pool is at level 4.

What follows for DbLive (from the code; verified through DbLive afterwards, see Notes):

1. **Between the scripts of one transaction.** DbLive sets the level on its connection only outside a transaction (`if (Transaction.Current is null)`, `MsSqlDA.cs:205`, `:437`). In a transaction the next connection gets the same session back from the pool of the transaction, at the level the script before it left. With `TransactionWrapLevel: Deployment` a `set transaction isolation level` of one script applies to the scripts after it and to the statements DbLive runs after it (the row of a migration); one of `setup.test.sql` applies to the test. The docs say that on SQL Server each script gets a reset session (`docs/llms-full.txt` §8.2, §10 "Session state between scripts", §16; `docs/project/configuration.md`, `docs/unit-testing/index.md`).
2. **To the application.** DbLive passes the connection string to SqlClient as it is (`MsSqlDA.cs:58`), so its connections go back to the pool of the user's connection string at the level DbLive left on them, and an application in the same process with the same connection string gets them. The level of a physical connection is the one of its last use: the self-deployment (`Serializable`, `DbLiveSelfDeployer.cs:14`) is followed by the deployment, which sets its own level on the same connection. A connection is left at another level than `READ COMMITTED`:
   - after `RunAllTestsAsync`: `TestsTransactionIsolationLevel`, `Serializable` by default;
   - after `DeployAsync` with a `TransactionIsolationLevel` other than `ReadCommitted`, or when the last script on a connection set a level of its own (BeforeDeploy, AfterDeploy, `TransactionWrapLevel: None`); the code deployment uses several connections in parallel;
   - after a transaction that failed, timed out or was rolled back by SQL Server.

   The autocommit statements of the application and its transactions without a level (EF Core `SaveChanges`) then run at that level: more blocking, more deadlocks. The cases: a deployment at the startup of a single-instance application (rule 20 allows it), SQL tests in the process of C# tests that use the same connection string.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs`: `ExecuteScriptAsync`, `ExecuteQueryMultipleAsync`, `SetScriptIsolationLevelAsync` (was `SetTransactionIsolationLevelAsync`)
- `src/DbLive/Common/TranIsolationLevelMapper.cs`

## Fix
Decided by the maintainer on 2026-11-24:
- Problem 1, in the code: DbLive sets the isolation level explicitly on SQL Server. In a transaction it is the level of the transaction (`Transaction.Current.IsolationLevel`), not the level of the item: SQL Server does not switch a started transaction to `SNAPSHOT`. Without a transaction it is the level of the command. A script then never runs at the level the script before it left, and "each script gets a reset session" is true.
- Problem 2, in the docs only: the deployment runs in a process of its own (the recommendation), and DbLive does not change the connection string to get a pool of its own. When DbLive runs in the process of the application with the same connection string (a deployment at startup, SQL tests next to C# tests that use the same connection string), the user gives the connection string of DbLive another `Application Name`: another connection pool, because SQL Server keeps the isolation level on a pooled connection.

- [x] One place sets the level: `SetScriptIsolationLevelAsync`, before every script (`ExecuteNonQueryAsync`, `ExecuteCodeItemAsync`, `ExecuteQueryMultipleAsync`). Not on the connections of DbLive's own queries of the `dblive` tables (Notes)
- [x] For a script in a transaction, the `SET` goes into `StartTransactionGuardSql`, which runs before each script anyway: no extra round trip? No: one command for both paths, an extra round trip per script in a transaction (Notes)
- [x] A `SET` of the same level in a `Snapshot` transaction does not fail (verified)
- [x] Docs: the level is set before each script (problem 1); another `Application Name` in the process of the application (problem 2)

## Docs to update
- [x] `docs/llms-full.txt` §8.2 (setup file), §10 ("Session state between scripts"), §16 (row "Session state between the scripts of one transaction"): DbLive sets the isolation level before each script
- [x] `docs/llms-full.txt` §11.1 (`SetDbConnection`), §8.2 (the recipe without xUnit), §16 (new row "A connection of DbLive back in the connection pool"): another `Application Name` when DbLive runs in the process of the application
- [x] `docs/project/configuration.md` (`Deployment`), `docs/unit-testing/index.md` (setup file, "A test database without xUnit")
- [x] `docs/deployment/index.md` ("A deployer"): the deployment in a process of its own, and the `Application Name` when it is not

## Done when
- [x] Verified through DbLive with `TransactionWrapLevel: Deployment`: a migration runs `set transaction isolation level serializable`, the next script reads the level of the transaction; the same for `setup.test.sql` and its test
- [x] Verified through DbLive: after `RunAllTestsAsync`, a `SqlConnection` with the same connection string reads `transaction_isolation_level` 4 of its session, and 2 when DbLive had another `Application Name` (what the docs tell the user)
- [x] Code fixed
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- PostgreSQL is not affected: Npgsql resets the session (`discard all`) when the connection goes back to the pool (verified in 0096-37), and PostgreSQL does not change the isolation level of a transaction after its first query (PostgreSQL documentation, not verified).
- Rejected by the maintainer on 2026-11-24: a pool of DbLive's own, with `GetConnection` changing `Application Name` (adding `DbLive` to it). The maintainer does not want DbLive to change the `Application Name` of the user: the deployment runs in a process of its own, and the docs tell the user what to do when it does not.
- Rejected: setting `READ COMMITTED` on the connection before it goes back to the pool. It does not cover a connection in a transaction that failed or timed out: commands on it fail until the scope is disposed, and it goes back to the pool at the level of the transaction (known behavior of SqlClient, not verified).
- Verified through DbLive on 2026-11-24 (SQL Server 2025, `SessionStateTests`), before the fix: in a `ReadCommitted` deployment transaction, after a migration with `set transaction isolation level serializable`, the next migration and the code file ran at 4 (serializable); in a `Serializable` one, after `read committed`, at 2; the test after a `setup.test.sql` with `repeatable read` at 3 instead of 4; in a `Snapshot` scope, the script after `read committed` at 2. After the fix: the level of the transaction in every case.
- Verified: `SET TRANSACTION ISOLATION LEVEL SNAPSHOT` in a transaction that began in snapshot does not fail, also after a script changed the level to `read committed`: the transaction goes back to snapshot (5).
- Verified: after a SQL test, a `SqlConnection` with the same connection string reads level 4 (the connection of the test from the pool), and with another `Application Name` level 2. A temporary table and `XACT_ABORT` of `setup.test.sql` do not reach the test.
- The level is set before the scripts, not on the connections of DbLive's own queries (`SaveCodeItemAsync`, `UpdateMigrationStateAsync`, ...). Those read and write only the rows of DbLive in the `dblive` tables, under the deployment lock, so their level changes no result; a `SET` on each would be one more round trip for every row DbLive reads or writes, and Dapper opens their connections itself.
- No `SET` in `StartTransactionGuardSql`: one method for a script with and without a transaction is simpler, and one round trip is nothing next to a script. Inside a transaction the script gets the level of `Transaction.Current`, mapped back by `TranIsolationLevelMapper.FromSystemTransaction`; a transaction that DbLive did not open with a level it does not know (`ReadUncommitted`) throws `ArgumentOutOfRangeException`, as DbLive opens every transaction itself.
- Found while working on it: every deployment with `Snapshot` and `TransactionWrapLevel: Deployment` fails on DbLive's own `create clustered index` on `#paths` (Msg 3964): 0096-164. The snapshot case of this task is tested through the data access for that reason.

## Result
- `MsSqlDA.SetScriptIsolationLevelAsync` sets the isolation level before every script on SQL Server: in a transaction the level of `Transaction.Current`, without one the level of the command, as before. Before, it was set only without a transaction, and a script in a transaction ran at the level the script before it set. `TranIsolationLevelMapper.FromSystemTransaction` maps the level of a transaction back.
- The leak of the level to an application in the same process is documented, not fixed: DbLive passes the connection string as it is; the docs tell the user to give DbLive another `Application Name` in that case (the maintainer's decision).
- Tests: `SessionStateTests` (SQL Server, new): the level of a migration does not reach the next migration and code file in the deployment transaction (two levels), a script in a snapshot transaction, `setup.test.sql` and its test, a temporary table and `XACT_ABORT` of `setup.test.sql`, and the pool after a SQL test with the same and with another `Application Name`. `TranIsolationLevelMapperTests`: `FromSystemTransaction`. The whole solution passes: `DbLive.Tests` 679, `DbLive.MSSQL.Tests` 166, `DbLive.PostgreSQL.Tests` 95, the demo tests.
- Docs: `docs/llms-full.txt` §8.2, §10, §11.1, §16; `docs/project/configuration.md`; `docs/unit-testing/index.md`; `docs/deployment/index.md`.
- Found: 0096-164.
