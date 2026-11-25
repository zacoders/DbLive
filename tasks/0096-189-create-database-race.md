---
type: bug
priority: low
created: 2026-11-26
---
# Two deployments that create the same database: the second fails with the error of the provider

## Problem
With `CreateDbIfNotExists`, `DeployAsync` creates the database before it takes the deployment lock (`src/DbLive/DbLive.cs:35-37`): the lock lives in the database, so it can be taken only once the database exists. `CreateDBAsync(skipIfExists: true)` checks, then creates (`src/DbLive.MSSQL/MsSqlDA.cs:697-714`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs:676-696`). Two deployments of a database that does not exist yet (two instances of an application that deploy at startup, two test processes with the same connection string) both find no database and both run `create database`. The second fails with a bare `SqlException` 1801 `Database 'X' already exists` (PostgreSQL `42P04`), instead of `DeploymentInProgressException`, which the deployment lock gives a second deployment (§12.1). Read in the code, not run.

Rule 18 keeps `CreateDbIfNotExists` to local and test databases, so the case is rare. The fixture builders give each fixture a new database name and are not affected.

## Fix
In `CreateDBAsync` with `skipIfExists`, catch "already exists" and go on as if the database existed: the deployment then gets to the lock and fails there with `DeploymentInProgressException`, or deploys after the first one.
- [ ] Which error does PostgreSQL give when the two `CREATE DATABASE` race: `42P04`, or `23505` on `pg_database_datname_index`? Verify with two connections.
- [ ] Can the second deployment connect while the first `create database` is still running, or does it fail to open the database (SQL Server Msg 4060)? Verify on both servers.

## Docs to update
- `docs/llms-full.txt` §11.3 (the `CreateDbIfNotExists` row), §12.1

## Done when
- [ ] Fixed, with a test of two deployments that create the same database at the same time, on both providers

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
