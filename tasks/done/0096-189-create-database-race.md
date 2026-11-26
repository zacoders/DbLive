---
type: bug
priority: low
created: 2026-11-26
closed: 2026-11-26
---
# Two deployments that create the same database: the second fails with the error of the provider

## Release note
With `CreateDbIfNotExists`, two deployments of a database that does not exist yet no longer fail on its creation. Both find no database and both create it; the one whose create loses goes on to the deployment lock and fails with `DeploymentInProgressException`, or deploys after the other one when that one has already ended, like any second deployment. Before, it failed with the error of the server: on SQL Server `A severe error occurred on the current command` or `Database '...' already exists`, on PostgreSQL `duplicate key value violates unique constraint "pg_database_datname_index"`. Nothing to change on upgrade.

## Problem
With `CreateDbIfNotExists`, `DeployAsync` creates the database before it takes the deployment lock (`src/DbLive/DbLive.cs:35-37`): the lock lives in the database, so it can be taken only once the database exists. `CreateDBAsync(skipIfExists: true)` checks, then creates (`src/DbLive.MSSQL/MsSqlDA.cs:697-714`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs:676-696`). Two deployments of a database that does not exist yet (two instances of an application that deploy at startup, two test processes with the same connection string) both find no database and both run `create database`. The second fails with a bare `SqlException` 1801 `Database 'X' already exists` (PostgreSQL `42P04`), instead of `DeploymentInProgressException`, which the deployment lock gives a second deployment (§12.1). Read in the code, not run.

Verified: the errors are others than read in the code. On SQL Server the create that loses fails more often with `A severe error occurred on the current command` (`SqlException` with `Number` 0) than with 1801; on PostgreSQL with `23505` on `pg_database_datname_index`, not with `42P04` (see Notes).

Rule 18 keeps `CreateDbIfNotExists` to local and test databases, so the case is rare. The fixture builders give each fixture a new database name and are not affected.

## Fix
In `CreateDBAsync` with `skipIfExists`, catch "already exists" and go on as if the database existed: the deployment then gets to the lock and fails there with `DeploymentInProgressException`, or deploys after the first one.
- [x] Which error does PostgreSQL give when the two `CREATE DATABASE` race: `42P04`, or `23505` on `pg_database_datname_index`? Verify with two connections. `23505` (verified, see Notes).
- [x] Can the second deployment connect while the first `create database` is still running, or does it fail to open the database (SQL Server Msg 4060)? Verify on both servers. It does not get there: both servers make it wait for the running create (verified, see Notes).

## Docs to update
- `docs/llms-full.txt` §11.3 (the `CreateDbIfNotExists` row), §12 (step 3), §12.1
- `docs/deployment/index.md` (section "One deployment at a time")

## Done when
- [x] Fixed, with a test of two deployments that create the same database at the same time, on both providers

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
- 2026-11-26, the maintainer: catch the error of the create and check again that the database exists (option 2 of the review), not by the error code.
- Experiments, on SQL Server 2025 and PostgreSQL 18 of the test containers, with throwaway tests that are not kept (verified):
  - 8 connections run `create database` of one name at the same time, 20 rounds. SQL Server: 20 creates succeed, of the 140 that lose 62 fail with 1801 and 78 with `A severe error occurred on the current command. The results, if any, should be discarded.` (`SqlException`, `Number` 0, `Class` 11, no error of the server). The connection stays open, and a query on it then finds the database. PostgreSQL: all 140 fail with `23505` `duplicate key value violates unique constraint "pg_database_datname_index"`, none with `42P04`; the connection stays open. So a catch of 1801 and `42P04` alone would miss most of them.
  - A create that takes 12 seconds (SQL Server: a log file of 3 GB; PostgreSQL: 1.4 seconds from a template with a table of 2 million rows) and another session meanwhile. SQL Server: `select ... from sys.databases where name = @name` waits for the create, then returns the database `ONLINE`; a second `create database` waits too and fails with 1801 when the first one ends; a connection to the database right after opens. PostgreSQL: the database is not in `pg_database` while it is created, and a connection to it fails with `3D000`; a second `CREATE DATABASE` waits for the first one to commit, then fails with `23505`; a connection right after opens. So the deployment that loses does not reach the database before it is created: no Msg 4060.
  - `42P04` comes when the second create starts after the first one committed, but after its check found no database: a narrower window, not seen in the runs. The fix does not depend on the code.
  - 8 `CreateDBAsync(skipIfExists: true)` started together behind a barrier, with the code before the fix, 10 rounds: 70 fail (59 severe errors, 11 times 1801).
- On SQL Server the check waits while another session creates the database, so only deployments that check before the first create starts lose the race. The test fills the pool of the connection string of `master` first; without that, the 8 deployments connect at different moments, the later ones find the database, and the test passed on the code before the fix.

## Result
- `MsSqlDA.CreateDBAsync` and `PostgreSqlDA.CreateDBAsync`: with `skipIfExists`, an error of the server from `create database` (`SqlException`, `PostgresException`) on a connection that is still open is followed by the check of the existence again; the database exists, so the create returns, and the deployment goes on to the lock. Otherwise the error is thrown as before. Not by the error code: SQL Server gives an error without a number for most of the creates that lose. The check is a private `DbExistsAsync` of each provider, used before the create too.
- Tests: `ConcurrentDeploymentTests.Deployments_that_create_the_database_at_the_same_time_deploy_or_fail_on_the_lock` of both providers: 8 deployments of a new database at the same time each deploy or fail with `DeploymentInProgressException`, at least one deploys, and the migration runs once. Before the fix it failed 3 of 3 runs on both providers (SQL Server with the severe error, PostgreSQL with `23505`); with the fix it passed 3 of 3. `dotnet test src/DbLive.sln`: all pass but one test of the deployment timeout, which fails under load (0096-191) and passed alone and in the next run of the whole SQL Server project (204 of 204).
- Docs: `docs/llms-full.txt` §11.3, §12 step 3 and §12.1; `docs/deployment/index.md` "One deployment at a time".
