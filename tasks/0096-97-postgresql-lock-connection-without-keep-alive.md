---
type: bug
priority: medium
created: 2026-11-17
---
# PostgreSQL: the connection of the deployment lock sends nothing while the deployment runs

## Problem
The deployment lock of PostgreSQL is a session advisory lock on a connection of its own (`Pooling = false`, `Enlist = false`, 0096-75). Between `pg_try_advisory_lock` at the start and `pg_advisory_unlock` at the end the connection sends nothing: it stays idle for the whole deployment, however long, while the scripts run on other connections. Npgsql sends no keep-alive by default (`Keepalive = 0`, `TcpKeepAlive = false`), and the server sends its first TCP keep-alive after `tcp_keepalives_idle`, by default that of the OS (2 hours on Linux). So nothing travels on the connection.

A NAT gateway, a firewall or a load balancer between the deployer and the server drops a TCP connection that stays idle longer than its timeout (from their documentation, not run: AWS NAT Gateway 350 seconds, Azure Load Balancer 4 minutes by default). A deployment that runs longer loses the connection of its lock, while the network works the whole time:

- **The device drops the connection silently** (AWS NAT Gateway). The server does not learn of it, and its session keeps the lock. At the end `pg_advisory_unlock` fails, and `DeploymentLock.DisposeAsync` drops the error; its comment says that the session ends with the broken connection, which is not so on the server. Every following deployment fails with `DeploymentInProgressException` until the keep-alive of the server ends the session, up to about 2 hours. §12.1 describes this for a deployer that lost its network, not for a deployment that succeeded.
- **The device resets both sides** (Azure Load Balancer with TCP reset on idle), or the server ends the idle session itself (`idle_session_timeout`, PostgreSQL 14 and later, 0 by default). The session ends in the middle of the deployment, and the lock with it. A second deployment can then start beside the running one: the case 0096-75 guards against.

SQL Server is not affected: the server sends a TCP keep-alive every 30 seconds by default (§12.1), and SqlClient does too (from memory, not verified), so a device on the way never sees the connection idle.

The lock is new in 2.0, so a long deployment through such a device worked with 1.5. From the code and the documentation of the devices, not reproduced.

Found by the second code review of 2026-11-17 (finding 1).

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:425-455` (`AcquireDeploymentLockAsync`: the builder of the lock connection)
- `src/DbLive/Adapter/DeploymentLock.cs:19-23` (the dropped error of the release, and its comment)
- `docs/llms-full.txt:860` (§12.1), `docs/deployment/index.md:127` ("A lock left by a lost deployment")

## Fix
Set a keep-alive on the lock connection when the connection string has none: `builder.KeepAlive = 30` (Npgsql sends a keep-alive query after 30 seconds without traffic), or `TcpKeepAlive` with `TcpKeepAliveTime`. A keep-alive set by the user stays.

- [ ] `KeepAlive` (a query of Npgsql) or TCP keep-alive? Does the keep-alive query of Npgsql count as activity for `idle_session_timeout`? Not checked.
- [ ] Log a warning when the release of the lock fails, instead of dropping the error? A lock left on the server would then show in the log of the deployment that left it.

## Docs to update
- `docs/llms-full.txt` §12.1 (the lock connection sends a keep-alive; a device with an idle timeout on the way)
- `docs/deployment/index.md` ("A lock left by a lost deployment")

## Done when
- [ ] The lock connection of PostgreSQL has a keep-alive unless the connection string sets one, with a test
- [ ] The comment of `DeploymentLock.DisposeAsync` says what happens when a device on the way dropped the connection
- [ ] Docs updated
