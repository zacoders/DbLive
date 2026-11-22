---
type: bug
priority: medium
created: 2026-11-17
closed: 2026-11-22
---
# PostgreSQL: the connection of the deployment lock sends nothing while the deployment runs

## Release note
None. The deployment lock is new in this release; the keep-alives of its connection on PostgreSQL are in the note of 0096-75.

## Problem
The deployment lock of PostgreSQL is a session advisory lock on a connection of its own (`Pooling = false`, `Enlist = false`, 0096-75). Between `pg_try_advisory_lock` at the start and `pg_advisory_unlock` at the end the connection sends nothing: it stays idle for the whole deployment, however long, while the scripts run on other connections. Npgsql sends no keep-alive by default (`Keepalive = 0`, `TcpKeepAlive = false`), and the server sends its first TCP keep-alive after `tcp_keepalives_idle`, by default that of the OS (2 hours on Linux). So nothing travels on the connection.

A NAT gateway, a firewall or a load balancer between the deployer and the server drops a TCP connection that stays idle longer than its timeout (from their documentation, not run: AWS NAT Gateway 350 seconds, Azure Load Balancer 4 minutes by default). A deployment that runs longer loses the connection of its lock, while the network works the whole time:

- **The device drops the connection silently** (AWS NAT Gateway). The server does not learn of it, and its session keeps the lock. At the end `pg_advisory_unlock` fails, and `DeploymentLock.DisposeAsync` drops the error; its comment says that the session ends with the broken connection, which is not so on the server. Every following deployment fails with `DeploymentInProgressException` until the keep-alive of the server ends the session, up to about 2 hours. §12.1 describes this for a deployer that lost its network, not for a deployment that succeeded.
- **The device resets both sides** (Azure Load Balancer with TCP reset on idle), or the server ends the idle session itself (`idle_session_timeout`, PostgreSQL 14 and later, 0 by default). The session ends in the middle of the deployment, and the lock with it. A second deployment can then start beside the running one: the case 0096-75 guards against.

SQL Server is not affected: the server sends a TCP keep-alive every 30 seconds by default (§12.1), and SqlClient does too (from memory, not verified), so a device on the way never sees the connection idle.

The lock is new in 2.0, so a long deployment through such a device worked with 1.5. From the code and the documentation of the devices, not reproduced.

Found by the second code review of 2026-11-17 (finding 1).

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs` (`AcquireDeploymentLockAsync`: the builder of the lock connection)
- `src/DbLive/Adapter/DeploymentLock.cs` (the dropped error of the release, and its comment)
- `docs/llms-full.txt` §12.1, `docs/deployment/index.md` ("A lock left by a lost deployment")

## Fix
Set a keep-alive on the lock connection when the connection string has none: `builder.KeepAlive = 30` (Npgsql sends a keep-alive query after 30 seconds without traffic), or `TcpKeepAlive` with `TcpKeepAliveTime`. A keep-alive set by the user stays.

- [x] `KeepAlive` (a query of Npgsql) or TCP keep-alive? Does the keep-alive query of Npgsql count as activity for `idle_session_timeout`? `KeepAlive`, and the TCP keep-alive of the server besides (decision of the maintainer, 2026-11-22). Yes, it counts: verified, see Notes.
- [x] Log a warning when the release of the lock fails, instead of dropping the error? Yes (decision of the maintainer, 2026-11-22).

## Docs to update
- `docs/llms-full.txt` §12.1 (the lock connection sends a keep-alive; a device with an idle timeout on the way)
- `docs/deployment/index.md` ("A lock left by a lost deployment")

## Done when
- [x] The lock connection of PostgreSQL has a keep-alive unless the connection string sets one, with a test
- [x] The comment of `DeploymentLock.DisposeAsync` says what happens when a device on the way dropped the connection
- [x] Docs updated

## Notes
- 2026-11-22, the maintainer chose: Npgsql `KeepAlive` on the client, plus the TCP keep-alive of the server set for the session of the lock (`tcp_keepalives_*` are settings any user may change), plus a warning in the log when the release fails. A TCP keep-alive of the client alone was not taken: the server does not see it, so it does not count for `idle_session_timeout`. A heartbeat of DbLive or a lock in a table was not taken: Npgsql does the same.
- The keep-alive of the server shortens the case of §12.1 "a deployer that lost its network" too: the session ends within about a minute (30 + 3 × 10 seconds), not after 2 hours, as on SQL Server. Also when a device on the way drops the connection anyway.
- Verified (PostgreSQL 18 in Testcontainers): with `Keepalive = 1` and `-c idle_session_timeout=2000` in the connection string, the session of the lock still holds the lock after 5 seconds idle; with the keep-alive of DbLive (30 seconds) instead, the session ends and the lock with it. So the keep-alive of Npgsql is activity for `idle_session_timeout`.
- `show tcp_keepalives_idle` prints `30`, without a unit.
- The warning is logged by `DbLive.DeployAsync`, not by `DeploymentLock`: the data access classes have no logger. `DeploymentLock.DisposeAsync` now closes the connection and throws the error of the release; `DbLive` catches it, so it does not replace the error of a failed deployment and does not fail one that did its work. The comment of that catch says what happens when a device on the way dropped the connection.
- Release note: users of 1.5 never had the lock, so this is no fix for them. The keep-alives went to the note of 0096-75, which introduces the lock, with "within about a minute" for a deployer that lost its network.
- The first version of the new tests deployed a database of their own, and another test of the run failed with `53300: sorry, too many clients already`. The PostgreSQL tests reach `max_connections` (100) already: 0096-103. The new tests now take the lock of the database `postgres`, which no other test deploys.

## Result
- `PostgreSqlDA.AcquireDeploymentLockAsync`: Npgsql `KeepAlive = 30` on the lock connection when the connection string sets none, and `set tcp_keepalives_idle = 30; set tcp_keepalives_interval = 10; set tcp_keepalives_count = 3;` on its session before `pg_try_advisory_lock`.
- `DeploymentLock.DisposeAsync` throws the error of the release after closing the connection; `DbLive.DeployAsync` releases the lock in `finally` and logs that error as a warning.
- Tests: `DbLiveTests` (a release error after a successful and after a failed deployment), `DeploymentLockTests` (the error is thrown after the connection is closed), PostgreSQL `ConcurrentDeploymentTests.Lock_connection_sends_keep_alives_both_ways` and `Keep_alive_of_the_connection_string_keeps_the_lock_through_idle_session_timeout`; the tests of a session that ended now expect the error of the release, on both providers.
- Docs: `docs/llms-full.txt` §12.1, `docs/deployment/index.md` ("A lock left by a lost deployment").
