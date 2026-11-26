---
created: 2026-11-26
---
# A project folder for database options, run before the deployment lock

## Idea
Database options (`alter database ... set ...`, filegroups, Query Store, `alter database scoped configuration`) become part of the project: a folder of their own, for example `DatabaseOptions/`, which DbLive runs on every deployment before anything else touches the database. The scripts are re-runnable and describe the state the database must have:

```sql
-- DatabaseOptions/01.rcsi.sql
if (select is_read_committed_snapshot_on from sys.databases where database_id = db_id()) = 0
    alter database current set read_committed_snapshot on with rollback immediate;
```

The step:
- runs after `CreateDbIfNotExists` (step 3 of §12) and before the deployment lock (step 4), on every deployment, whatever the `DeployParameters`;
- runs each file on a connection of the pool, without a transaction, also with `TransactionWrapLevel: Deployment` (the deployment transaction begins later);
- records nothing in the `dblive` tables (the tables may not exist yet): no new stored value, `MinCompatibleVersion` stays;
- clears the connection pools of DbLive before it (the idle connections of an earlier deployment in the same process: `[SqlDeployFact]`, a deployment at the startup of an application; at least the pool of the connection string and the one with `ConnectRetryCount = 0`, `MsSqlDA.cs:727`) and after it (on PostgreSQL `alter database ... set` reaches only new sessions; without it the code of the same deployment runs with the old `search_path`);
- runs the read-only ProjectId check before it, so that a wrong connection string fails with `ProjectIdMismatchException` before the options of another project's database change (the check runs again after the lock, as now);
- has a short timeout of its own (10 minutes?): an option change is instant, and one that waits for other sessions needs `rollback immediate` or `no_wait`.

At that moment DbLive has no session in the database: the lock is not taken, the pools are cleared. An option that needs the database to itself (`READ_COMMITTED_SNAPSHOT`, `ENABLE_BROKER`) runs at once on a database without other users (local, CI, the test databases of `DbLive.xunit`, `[SqlDeployFact]`). In production the author of the script decides what happens to the sessions of the application: `with rollback immediate`, `with rollback after N seconds`, or `with no_wait`, which then fails only when the application is connected. The deployment lock is not involved, so `rollback immediate` no longer ends it.

Same approach as a DbUp deployer of the maintainer: a separate DbUp run before the main one, `DatabaseConfiguration.sql`, `NullJournal` (every deployment), `WithoutTransaction()`, 10 minutes timeout.

Came up 2026-11-26, from 0096-183.

## Why
- An option that needs the database to itself cannot run in a deployment now: it waits for the session of the deployment lock and the idle connections of the pool until its timeout (`MigrationTimeout`, 12 hours). With `rollback immediate` it runs, but ends the session of the lock: the rest of the deployment runs without the lock, and the end logs "its connection is broken" (verified in 0096-183).
- With `TransactionWrapLevel: Deployment` no `alter database` runs at all on SQL Server: BeforeDeploy and the migrations run in the deployment transaction, and an item `None` is ignored (follows from §5.3 and §10, not run).
- Without it the options live outside the project (a script run before `DeployAsync`), and the test databases of `DbLive.xunit` and `[SqlDeployFact]` do not get them: the tests run on server defaults, and the options scripts are not tested before the release.

## Not in 2.0.0
Decision of the maintainer, 2026-11-26: not needed in 2.0.0. Adding it later breaks nothing: a folder `DatabaseOptions` in a project fails today with `FilesOutsideProjectFoldersException`, so no project has one, and the step writes nothing to the `dblive` tables. Until then 0096-183 documents the workaround: `with rollback immediate` in a migration with `TransactionWrapLevel: None`.

## Options at creation
`CreateDbIfNotExists` runs a bare `create database [name]` (`MsSqlDA.cs:706`, `PostgreSqlDA.cs:693`): a local or test database gets the server defaults, not the options of production. A key in `settings.json`, for example `CreateDatabaseOptions`, as text appended to `create database [name]`:
- SQL Server: `collate Latin1_General_100_CS_AS with catalog_collation = SQL_Latin1_General_CP1_CI_AS`
- PostgreSQL: `encoding 'UTF8' locale_provider icu icu_locale 'und' template template0`

A production database is created by a DBA, not by DbLive (rule 18 of §0). The options folder checks it then, and a database created wrong fails the deployment before the migrations:

```sql
if convert(sysname, databasepropertyex(db_name(), 'Collation')) <> N'Latin1_General_100_CS_AS'
    throw 50000, N'The database must have collation Latin1_General_100_CS_AS.', 1;
```

A new database gets its collation from the folder too: the step runs before the self-deployment, when the `dblive` tables do not exist yet. On an existing database `dblive.version.one_row_lock` (a computed column) blocks `alter database ... collate` with Msg 5075 (`DatabaseCollationChangeTests`), and so do the computed columns, check constraints and schema-bound objects of the user.

## What stays impossible
- Options after which the deployment cannot go on: `OFFLINE`, `READ_ONLY`, `SINGLE_USER` / `RESTRICTED_USER` as the end state, `EMERGENCY`, `MODIFY NAME` (the connection string names the old name). They are operations, not a state of the project. `single_user ... multi_user` within one script works.
- Server configuration (`sp_configure`, tempdb): not an option of the database.
- PostgreSQL: `alter database ... set tablespace` and `rename` of the current database cannot run from a session in it.
- Restrictions of the platform: Azure SQL Database does not support some options, an availability group requires `RECOVERY FULL`.

## Considered
- **Run the folder on the session of the deployment lock, after the lock and the self-deployment.** The session that runs `alter database` does not block itself, and `rollback immediate` ends every other one, so the lock stays; the step is protected by the lock like the rest. Costs more: the data access must run a script on the connection of the lock (now every script gets a connection of the pool), in both providers; the files share one session without a reset (a `set`, a temporary table or a `use` of one file reaches the next one); a run recorded in `dblive.folder_items` is a new `folder_type` value. Gains only protection against two deployments at once, which the pipeline prevents (`concurrency`, §18.10).
- **Before the lock (this idea): the cost.** Two deployments at once (an operator error): the options step of the second runs before it fails on the lock, and its `rollback immediate` can end the sessions of the running one, the lock included. §12.1 says the second deployment fails "before it reads or changes anything": it becomes "after the database options step".
- **Declarative keys** (`"ReadCommittedSnapshot": true`): dozens of options in two providers and several server versions, read from different places (`sys.databases`, `sys.database_scoped_configurations`, Query Store). A script covers every option.
- **The lock in `master`, or `[db].sys.sp_getapplock` from `master`**, so that the session of the lock is not in the database: Azure SQL Database runs neither, nor do contained users; it changes the lock, and an older DbLive would not see the lock of a newer one.
- **BeforeDeploy on the session of the lock**: with `Deployment` BeforeDeploy runs in the deployment transaction (all or nothing), which this would break.
- **Migrations with `None` on the session of the lock**: they lose the reset of the session between scripts (§10), and with `Deployment` still do not run. One place for options is simpler.

## To verify
- [ ] `alter database current set read_committed_snapshot on` (and `enable_broker`, `accelerated_database_recovery = on`) right after `ClearPool`, without `rollback immediate`: runs without waiting. The server ends the session of a closed connection asynchronously (`DeploymentLock.cs`), so it may wait a moment for the cleared connections.
- [ ] `with no_wait`: fails at once with a session of another program in the database, runs without one.
- [ ] PostgreSQL: after `alter database ... set search_path` in the folder and `ClearPool`, a code file of the same deployment creates its objects in the new schema.

## Open questions
- [ ] A folder (files in the order of their names, as BeforeDeploy) or one file, as in the DbUp deployer? The name: `DatabaseOptions`, with a setting to rename it as `BeforeDeployFolder`?
- [ ] Every deployment (drift is corrected; a `rollback immediate` without `if` ends the sessions of the application on every deployment, so the docs require the check) or only when the file changed, by hash as the Code folder?
- [ ] The timeout by default: 10 minutes?
- [ ] `CreateDatabaseOptions` together with the folder, or separately?
- [ ] The collation of an existing database: replace the computed `dblive.version.one_row_lock` by a self-migration, or leave collation to the creation of the database?

## See also
- `tasks/done/0096-183-mssql-alter-database-waits-for-dblive-sessions.md`: the problem, verified, and the workaround documented for 2.0.0
