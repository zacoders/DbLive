---
type: bug
priority: medium
created: 2026-11-12
closed: 2026-11-16
breaking: true
---
# Two deployments at the same time apply the same migration twice

## Release note
**One deployment of a database at a time.** A deployment now locks its database from the start to the end, also when it fails, and a second deployment of the same database that starts meanwhile fails at once with `DeploymentInProgressException` (new), before it reads or changes anything. Before, both deployments ran the new migrations: a migration that changes data ran twice without an error. The second deployment does not wait for the first. Deployments of different databases do not block each other. To upgrade: let your pipeline run one deployment of a database at a time (in GitHub Actions a `concurrency` group with `cancel-in-progress: false`), and do not call `DeployAsync` at the startup of an application that runs in several instances: deploy in a step before it starts. On PostgreSQL the lock belongs to a session: deploy directly or through PgBouncer in session mode, not in transaction pooling mode. A deployer machine that loses its network leaves the lock held until the server ends its session; "One deployment at a time" in the deployment docs shows how to find and end that session.

## Problem
The case needs two deployments of one database that run at the same time: two runs of a pipeline without a concurrency limit, or an application that calls `DeployAsync` at startup and starts in several instances. DbLive does not guard against operator mistakes (decision on 0096-41, 2026-11-07), so first decide whether this is one. The docs do not say that deployments must not overlap, and other migration tools take a lock here (Flyway, Liquibase, EF Core since 9), so a user does not expect it.

`DeployAsync` takes no lock. Each deployment reads the applied versions, then runs every version that is not in the list. Two deployments read the same list and both run the new migration. The transaction of a migration does not help: the second one waits for the locks of the first and then runs the same script again.

Verified on SQL Server 2022 (a temporary harness, not kept), with the default settings (`TransactionWrapLevel: Migration`, `ReadCommitted`): the database has migration 001; two builders deploy the project with a new `002.insert.sql` at the same time:

```sql
insert into dbo.Counter (Note) values ('migration 002');
waitfor delay '00:00:02';
```

Both deployments complete without an error, and `dbo.Counter` has two rows.

A DDL migration usually fails in the second deployment ("column names in each table must be unique"), which is only noise. A data migration (`insert`, `update ... set x = x * 1.1`) runs twice without an error.

Only the self-deployment is safe: it runs in one `Serializable` transaction, so one of two self-deployments waits or fails.

Found by the code review of 2026-11-12 (finding 2).

## Where
- `src/DbLive/DbLive.cs:16-47` (`DeployAsync`: no lock)
- `src/DbLive/Deployers/Migrations/MigrationsDeployer.cs:35-46` (`GetMigrationsToApplyAsync`: the applied versions are read once, before the migrations run)
- `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs:69-87` (the breaking scripts: the same read)

## Fix
- [x] Is a second deployment at the same time an operator mistake? If yes: only the docs change (§18.10: one deployment of a database at a time, with the pipeline setting that gives it, such as `concurrency` in GitHub Actions; never `DeployAsync` at the startup of an application with several instances). No: DbLive guards (decision of 2026-11-16 in Notes).
- [x] If DbLive guards: a lock of the database for the whole deployment, taken before the ProjectId check and held on its own connection outside the deployment transactions: `sp_getapplock` with `@LockOwner = 'Session'` on SQL Server, `pg_advisory_lock` on PostgreSQL. Does the second deployment wait, or fail at once with a clear exception? Failing at once is simpler and never runs a deployment that nobody watches. Fails at once (decision of 2026-11-16).

## Docs to update
- `docs/llms-full.txt` §0 (rule 20), §12 (step 4, new §12.1 "One deployment at a time"), §17 (`DeploymentInProgressException`), §18.10 (CI/CD pipeline)
- `docs/deployment/index.md` ("What a deployment does": step 3; new section "One deployment at a time")

## Done when
- [x] Decision of the maintainer recorded in Notes
- [x] Docs say that deployments of one database must not overlap, or the second deployment waits or fails, with an integration test on both engines

## Notes
2026-11-16, decision of the maintainer: DbLive guards. Unlike 0096-41, the docs never forbade overlapping deployments, a pipeline without a concurrency limit is the default of GitHub Actions, and the result is silent (a data migration applied twice), not an error. The deployment takes a lock of the database for its whole run, and a second deployment fails at once with a clear exception, it does not wait. Waiting would not keep the order of the builds: when the deployments of builds N and N+1 overlap and N+1 takes the lock first, N would run after it and either fail with `DatabaseAheadOfProjectException` or, after a code-only release, deploy the code of N over it.

How the lock behaves (from the documentation of the engines, to check by the tests):
- It belongs to the session, not to a transaction: `sp_getapplock @LockOwner = 'Session'`, `pg_try_advisory_lock`. It is released by the explicit release, or when the session ends. A crashed process (an exception, `kill -9`, an OOM kill) closes its socket, and the server ends the session and releases the lock at once.
- A machine or a network that disappears without closing the socket leaves the session to the TCP keep-alive of the server: SQL Server checks after 30 seconds by default, PostgreSQL after the keep-alive time of the OS (2 hours on Linux) unless `tcp_keepalives_idle` is set. Until then a new deployment fails; the docs say how to find and end the session that holds the lock.
- A pooled connection keeps its session after `Close`, and the lock with it, until the pool reuses or closes the connection. So the lock connection is not pooled and not enlisted in an ambient transaction, and the lock is released explicitly before it is closed: the session of a closed socket ends asynchronously on the server, and a deployment right after (the deployment test runs three in a row) would find the lock still held.
- Both locks are local to the database, so deployments of different databases on one server do not block each other. Two projects in one database without a `ProjectId` deploy one at a time.
- PostgreSQL through PgBouncer in transaction mode: a session lock means nothing there, the client session is not bound to one server connection. The docs say to deploy directly or through session mode.

Verified by the integration tests (SQL Server 2025, PostgreSQL 18):
- The scenario of the Problem: with the lock removed from `DbLive.DeployAsync` (SQL Server), the second deployment does not fail: it starts migration 002 too and waits on the gate of the test, and the test fails on its 30-second wait. With the lock it fails at once with `DeploymentInProgressException`, and `Counter` has one row.
- The queries of the docs find the session that holds the lock (`sys.dm_tran_locks` with `resource_description like '%:\[dblive.deployment\]:%'`; `pg_locks` with `classid = 25698, objid = 1818850917, objsubid = 1`), and `kill` / `pg_terminate_backend` of that session frees the lock for the next deployment.
- On PostgreSQL `pg_stat_activity` read inside a transaction keeps the snapshot of its first read: the test polls it on a connection of its own.

Not done: a session of the lock that breaks during a deployment releases the lock while the deployment goes on on its other connections, and DbLive does not notice. The deployment connections usually break with it.

A race the lock does not cover: two first deployments with `CreateDbIfNotExists` both find the database missing, and the second `create database` fails with the error of the server. Loud, and only for a new database.

Seen once while running the whole solution (all test projects in parallel): `CodeRetryInDeploymentTransactionTests.Code_file_that_outlives_the_deployment_timeout_fails_the_deployment_with_its_path` (SQL Server, a 2 second `DeploymentTimeout`) failed; it passed alone three times and in two runs of the whole MSSQL project. The lock is taken before the deployment transaction and does not count into its timeout; the test is sensitive to the load of the machine.

## Result
- `DbLive.DeployAsync` takes the deployment lock once the database exists (after `RecreateDatabase`, `CreateDbIfNotExists` or the existence check), before the ProjectId check, and holds it to the end with `await using`, also when the deployment fails.
- `IDbLiveDA.AcquireDeploymentLockAsync` (new) opens a connection of its own (`Pooling = false`, `Enlist = false`) and takes the lock without waiting: SQL Server `sp_getapplock @Resource = 'dblive.deployment', @LockOwner = 'Session', @LockTimeout = 0` (`-1` → `DeploymentInProgressException`, another negative result → `InternalException`); PostgreSQL `pg_try_advisory_lock(0x64626C697665)`. It returns a `DeploymentLock` (new, internal): its `DisposeAsync` releases the lock explicitly (`sp_releaseapplock`, `pg_advisory_unlock`), ignores an error of the release (a broken connection; it would replace the error of the deployment), and closes the connection.
- `DeploymentInProgressException` (new, public, `DatabaseName`, `Server`): the message says that this deployment changed nothing, to run it again after the other one, to limit the pipeline, and points to "One deployment at a time" for a lock left by a lost session.
- Decisions: DbLive guards, and the second deployment fails at once, it does not wait (maintainer, 2026-11-16). `breaking: true`: a pipeline or an application whose deployments overlapped now fails one of them and has to be changed.
- No change of the `dblive` tables: `MinCompatibleVersion` stays.
- Public API: `DbLive.DeploymentInProgressException`, `DatabaseName`, `Server` in `PublicAPI.Shipped.txt`.
- Tests: `DbLiveTests` (the lock is taken after `DropDbAsync`/`CreateDBAsync` and before the ProjectId check, disposed after the deployment and when it fails; `DeploymentInProgressException` stops the deployment before the ProjectId check, its message); `DeploymentLockTests` (release before close; an error of the release is not thrown and the connection is closed); `ConcurrentDeploymentTests` on SQL Server and PostgreSQL (the scenario of the Problem with a gate the migration waits on; the lock is released after a failed deployment; the lock of a session ended by `kill` / `pg_terminate_backend`, found with the queries of the docs, is free for the next deployment). `DbLive.Tests` 506 pass, `DbLive.MSSQL.Tests` 85, `DbLive.PostgreSQL.Tests` 60, the demo test projects pass; the solution builds without warnings.
- Docs: `docs/llms-full.txt` §0 rule 20, §12 step 4 and §12.1, §17, §18.10; `docs/deployment/index.md` (step 3 of "What a deployment does", "One deployment at a time" with the GitHub Actions `concurrency` block, the queries to find and end the session of a lost deployment, PgBouncer).
