---
type: bug
priority: medium
created: 2026-11-12
---
# Two deployments at the same time apply the same migration twice

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
- [ ] Is a second deployment at the same time an operator mistake? If yes: only the docs change (§18.10: one deployment of a database at a time, with the pipeline setting that gives it, such as `concurrency` in GitHub Actions; never `DeployAsync` at the startup of an application with several instances).
- [ ] If DbLive guards: a lock of the database for the whole deployment, taken before the ProjectId check and held on its own connection outside the deployment transactions: `sp_getapplock` with `@LockOwner = 'Session'` on SQL Server, `pg_advisory_lock` on PostgreSQL. Does the second deployment wait, or fail at once with a clear exception? Failing at once is simpler and never runs a deployment that nobody watches.

## Docs to update
- `docs/llms-full.txt` §12 (deployment pipeline), §18.10 (CI/CD pipeline), §17 (the exception, if DbLive guards)
- `docs/deployment/index.md`

## Done when
- [ ] Decision of the maintainer recorded in Notes
- [ ] Docs say that deployments of one database must not overlap, or the second deployment waits or fails, with an integration test on both engines
