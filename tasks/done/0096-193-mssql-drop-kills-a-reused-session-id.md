---
type: bug
priority: high
created: 2026-11-26
closed: 2026-11-26
---
# SQL Server: the drop of a test database can kill a session of another database

## Release note
None. A bug of the faster drop of 0096-105, not in an earlier release.

## Problem
`MsSqlDA.KillSessionsAsync` (`src/DbLive.MSSQL/MsSqlDA.cs`, added in 0096-105) reads the IDs of the sessions in the database to drop, then runs `kill <id>` for each, one command after another. SQL Server gives the ID of an ended session to the next new one. A session found in the database can end before its kill: the connection of the deployment lock, which `DeployAsync` closes at its end, so `DeploymentTest` drops right after it; or a connection closed just before the drop. The kill then ends the new session that took the ID, in another database.

On a server that several test classes use at once, as the container of `MsSqlContainerFixtureBuilder` or a server of `MsSqlFixtureBuilder`, the drop at the end of one `DbLiveTestFixture` or `DeploymentTest` could fail a test of another class with `A transport-level error has occurred when receiving results from the server.`

Seen on CI (PR #98): `DeploySqlIntegrationFullRepeatTests` (a SQL test failed with the transport-level error, run 45765172536) and `SqlTestTransactionTests` (the connection of the deployment lock terminated, run 45691062448). Verified with a test: without the fix, the kill of the ID of a session of `master` ends it with that error (`MsSqlDADropDbTests.Kill_spares_a_session_of_another_database_that_has_the_id_of_a_session_found_in_the_database`).

## Fix
`KillSessionAsync` kills in one batch: `if exists (select 1 from sys.dm_exec_sessions where session_id = @session and database_id = db_id(@name) and is_user_process = 1)` then `kill` by dynamic SQL. The ID is checked again on the server right before its kill, so it can change hands only between two statements of one batch, not between the read and a later command.

## Done when
- [x] Fixed, with a test that fails without the fix
- [x] The drop tests pass

## Notes
- Found while looking at the failed checks of PR #98 (2026-11-26). Most runs of the branch since 2026-11-25 failed on another SQL Server test each time. The other failures are not this bug:
  - `CodeFileTransactionTests.Code_file_whose_later_batch_fails_leaves_none_of_its_batches` (runs 45668658481, 45756652051, 45761755450; at de62940 before 0096-183, and once both cases at the same time): a session with an open transaction in the database of the test after the deployment. The test counted every session, also background tasks of SQL Server; it counts user sessions now. Inferred, not reproduced.
  - Timing under the load of all test projects at once: `MsSqlDADropDbTests.Drops_a_database_in_use_without_waiting_for_rollback_immediate` (2186 ms over its 2 s), `CommandTimeoutTests.Code_file_whose_time_runs_out_between_two_batches_fails_the_same_way` (a window of 0.4 s), the `drop login` at the end of `MsSqlDADropDbTests.Login_that_may_not_kill_sessions_drops_a_database_in_use` and of `DeploymentLoginPermissionsTests` (30 s command timeout). Not changed.
  - The SQL Server 2025 container of `AdventureWorks.Tests` crashed at its start (`handle-crash.sh`, runs 45756652051, 45765416504), before DbLive ran anything. Not changed.

## Result
`MsSqlDA.KillSessionAsync` checks that the session is still in the database and kills it in one batch; `KillSessionsAsync` calls it for each session it found. The new test opens a session of `master`, kills its ID as a session of the database to drop (the session lives), then a session of the database (it is killed); the test connections set `ConnectRetryCount = 0`, or SqlClient would open a new session for a killed idle connection unnoticed. Without the fix the test fails with the transport-level error of CI. `CodeFileTransactionTests` counts only user sessions with an open transaction.
