---
type: bug
priority: medium
created: 2026-11-22
---
# The drop of a database on SQL Server takes about 3 seconds: `single_user` waits for Query Store

## Problem
`MsSqlDA.DropDbAsync` runs `alter database [<db>] set single_user with rollback immediate;` and then `drop database [<db>];`, synchronously, through `ServerConnection.ExecuteNonQuery` of SMO: the method is async, but the wait holds a thread of the pool. On SQL Server 2022 and later Query Store is on for a new database (`model` has `is_query_store_on = 1`), and `set single_user` waits for it to stop: about 2.5-2.8 seconds per database, also when no session uses the database. The drop itself takes 40 ms.

Verified on 2026-11-22 in a container of `mcr.microsoft.com/mssql/server:2025-latest`, the image of the tests, averages of 5-10 runs on an empty database:
- `set single_user with rollback immediate`: 2824 ms; `drop database`: 43 ms; `create database`: 306 ms.
- `set restricted_user with rollback immediate`: 2565 ms, the same wait.
- `set query_store = off`, then `set single_user with rollback immediate` and `drop database`: 107 ms together; `single_user` alone after `query_store = off`: 33 ms.
- `kill` of every session in `sys.dm_exec_sessions` of the database, then `drop database`: 51 ms; with a session that holds an open transaction in the database, 44 ms.

Who waits:
- Users: `DbLiveTestFixture` with `dropDatabaseOnComplete: true` once per test class, `DeploymentTest` with `dropDatabaseOnComplete: true` once per test, `DeployParameters.RecreateDatabase` (internal) once per deployment.
- The recipe of the docs for tests without `DbLive.xunit` drops the database with `single_user with rollback immediate` too (`docs/unit-testing/index.md`, `docs/llms-full.txt`): the same wait in the code of the users.
- This repository: since 0096-104 every test of `DbLive.MSSQL.Tests` with a database of its own drops it. A full run took 65-68 s against 41 s with the drop turned off and 43-47 s before 0096-104 (verified on 2026-11-22, 155 tests, 5 runs each). The tests that depend on the timer of System.Transactions run beside the drops, and a held thread of the pool can delay the timer.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs` (`DropDbAsync`)
- `docs/unit-testing/index.md` and `docs/llms-full.txt` (the recipe of tests without `DbLive.xunit`)

## Fix
- [ ] Which drop: `set query_store = off` before `set single_user with rollback immediate`, or `kill` of the sessions of the database instead of `single_user`? The first keeps the statement that ends the sessions and the transactions they hold; the second needs the permission to `kill` (`ALTER ANY CONNECTION`), and a session that connects between the `kill` and the `drop` fails the drop.
- [ ] Run the statements with `SqlCommand.ExecuteNonQueryAsync` instead of `ServerConnection.ExecuteNonQuery`, so the drop holds no thread while it waits.
- [ ] Change the recipe of the docs the same way?

## Docs to update
- `docs/llms-full.txt` (the recipe of tests without `DbLive.xunit`), if the recipe changes
- `docs/unit-testing/index.md` (the same recipe)

## Done when
- [ ] `DropDbAsync` on SQL Server takes well below a second for a database without sessions, checked on the image of the tests
- [ ] A full run of `DbLive.MSSQL.Tests` takes about as long as before 0096-104
