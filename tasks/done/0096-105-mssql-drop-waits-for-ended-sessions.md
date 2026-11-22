---
type: bug
priority: medium
created: 2026-11-22
closed: 2026-11-23
---
# The drop of a database on SQL Server takes about 3 seconds: `rollback immediate` waits for the sessions it ends

## Release note
The fixtures of `DbLive.xunit` drop a test database on SQL Server about 3 seconds faster: `DbLiveTestFixture` with `dropDatabaseOnComplete: true` once per test class, `DeploymentTest` with `dropDatabaseOnComplete: true` once per test. `single_user with rollback immediate` waited about 3 seconds for the sessions it ended, also for an idle connection of the pool; the drop now kills the sessions of the database first. The kill needs `VIEW SERVER STATE` and `ALTER ANY CONNECTION` (`sa` of a container has them); without them the drop works as before. Nothing to change on upgrade.

## Problem
`MsSqlDA.DropDbAsync` ran `alter database [<db>] set single_user with rollback immediate;` and then `drop database [<db>];`, synchronously, through `ServerConnection.ExecuteNonQuery` of SMO: the method is async, but the wait held a thread of the pool. `single_user with rollback immediate` waits about 2.8-3 seconds as soon as it has to end a session in the database, whatever the session does: idle in the connection pool, in an open transaction, in a running query, or a connection closed just before whose session is still ending on the server. Without a session it takes 40-100 ms. The drop itself takes 40 ms.

Verified on 2026-11-23, see Notes: on SQL Server 2025 and 2022 in Docker (Linux) and on LocalDB 2025 (Windows). The task was opened with Query Store as the cause; the experiments refuted it.

Who waits:
- Users: `DbLiveTestFixture` with `dropDatabaseOnComplete: true` once per test class, `DeploymentTest` with `dropDatabaseOnComplete: true` once per test, `DeployParameters.RecreateDatabase` (internal) once per deployment. DbLive leaves an idle connection in the pool, so the wait comes almost always.
- The recipe of the docs for tests without `DbLive.xunit` drops the database with `single_user with rollback immediate` too, once per run.
- This repository: since 0096-104 every test of `DbLive.MSSQL.Tests` with a database of its own drops it. A full run took 65-68 s against 41 s with the drop turned off and 43-47 s before 0096-104 (verified on 2026-11-22, 155 tests, 5 runs each).

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs` (`DropDbAsync`, `CreateDBAsync`)
- `docs/unit-testing/index.md` and `docs/llms-full.txt` (the recipe of tests without `DbLive.xunit`)

## Fix
- [x] Which drop: `set query_store = off` before `set single_user with rollback immediate`, or `kill` of the sessions of the database instead of `single_user`? Neither as asked: `query_store = off` does not help (Notes). The drop kills the user sessions of the database, waits until no other session holds a lock on it, then runs `single_user with rollback immediate` and `drop database` as before. `single_user` stays for the sessions the kill does not end, so the two objections to the kill go away: a login without the permission drops as before, and a session that connects after the kill is ended by `single_user`.
- [x] Run the statements with `ExecuteAsync` of Dapper on `SqlConnection` instead of `ServerConnection.ExecuteNonQuery`, so the drop holds no thread while it waits. `CreateDBAsync` the same.
- [x] Change the recipe of the docs the same way? No (the maintainer, 2026-11-22): it drops once per run, before the deployment, and a kill loop in a recipe costs more than 3 seconds per run.

## Docs to update
- `docs/llms-full.txt` §13.2: how the drop of the fixtures ends the sessions, the permissions of the kill
- `docs/unit-testing/index.md` (section "SQL tests", the bullet of the drop): the same
- The recipe of tests without `DbLive.xunit` stays.

## Done when
- [x] `DropDbAsync` on SQL Server takes well below a second for a database without sessions, checked on the image of the tests: 40-190 ms, also with sessions
- [x] A full run of `DbLive.MSSQL.Tests` takes about as long as before 0096-104: 42-44 s

## Notes
- 2026-11-22, the maintainer: do `query_store = off` and the async statements, after checking `query_store = off` with live sessions; the recipe stays.
- Verified on 2026-11-23, SQL Server 2025 CU11 in `mcr.microsoft.com/mssql/server:2025-latest`, 3-5 runs each, `single_user with rollback immediate` + `drop database` in one batch:
  - a pooled idle session, an open transaction, a running query: 2830-2890 ms each. A session of the database is enough, its state does not matter.
  - `set query_store = off` first: 40 ms for itself, then the same 2900 ms. Query Store is not the cause.
  - no session at all (every connection without pooling, closed some time before): 65-80 ms, with Query Store on, off since the creation or off right before the drop, 0-5 seconds after the last use.
  - a non-pooled connection closed right before the drop: its session is still ending on the server, and `single_user` waits 2.8 s for it as for a live one.
  - `sys.dm_exec_requests` of the `alter database` during the wait: `PRINT_ROLLBACK_PROGRESS`, about 2.8 s.
  - `kill` of an idle session: it is gone from `sys.dm_exec_sessions` after 70 ms; `kill` + `drop database` in one batch: 106-128 ms.
  - the session that runs `set single_user` from `master` holds the single user of the database: a `drop database` from another connection fails with 3702. Both stay in one batch on one connection.
- The same on SQL Server 2022 CU22 in Docker (2830-2880 ms, `query_store = off` does not help, `kill` + `drop` 50 ms) and on LocalDB 2025 CU3 on Windows (3030 ms). On LocalDB a system session can be in the database: `kill` of it fails with 6107, hence `is_user_process = 1`.
- The fix measured the same way, kill + wait for the locks + `single_user` + `drop`: 140-190 ms on SQL Server 2025, 40-90 ms on LocalDB, for each case above and for three sessions at once; the wait for the killed sessions took 20-120 ms.
- A login with `dbcreator` only: `sys.dm_exec_sessions` shows it its own session only, and `sys.dm_tran_locks` fails with error 300 (`VIEW SERVER PERFORMANCE STATE` on SQL Server 2022 and later). Any `SqlException` of the kill step leaves the sessions to `single_user`, as before the change.
- The wait for the killed sessions ends after 2 seconds: a session that takes longer (a long rollback) is left to `single_user`, which waits for it anyway.
- `ServerConnection.StatementTimeout` of SMO is 600 seconds, the default of `SqlCommand` is 30: the create and the drop keep 600 explicitly (verified with SMO 180.10.0).
- `DeploymentLoginPermissionsTests.DropLoginAsync` kills the sessions of a login and waits for them the same way, for `drop login`.

## Result
- `MsSqlDA.DropDbAsync` kills the user sessions of the database (`sys.dm_exec_sessions`, `is_user_process = 1`), waits up to 2 seconds until no other session holds a lock on it (`sys.dm_tran_locks`), then runs `single_user with rollback immediate` and `drop database` in one batch. The kill step is best effort: without the permissions it does nothing and `single_user` ends the sessions as before. `CreateDBAsync` and `DropDbAsync` run their statements with `ExecuteAsync` of Dapper, with the timeout of SMO, 600 seconds; `ServerConnection` stays only for the split of `GO` batches.
- Tests: `MsSqlDADropDbTests`: a database with an idle pooled connection, an open transaction and a running query is dropped in less than 2 seconds (it fails on the code before the change), and a `dbcreator` login drops its database in use.
- A full run of `DbLive.MSSQL.Tests` on 2026-11-23: 42-44 s over 3 runs (157 tests), 69 s with the code before the change (155 tests).
- Docs: the drop of the fixtures and the permissions of the kill in `docs/llms-full.txt` §13.2 and `docs/unit-testing/index.md`. The recipe of tests without `DbLive.xunit` is unchanged.
