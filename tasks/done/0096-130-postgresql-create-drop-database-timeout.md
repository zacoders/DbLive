---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# PostgreSQL: `create database` and `drop database` keep the default timeout of 30 seconds

## Release note
PostgreSQL: creating the database (`CreateDbIfNotExists`, `RecreateDatabase`) and dropping it (`RecreateDatabase`, the test fixtures and `[SqlDeployFact]`) may take up to 10 minutes, as on SQL Server. Before, Npgsql stopped them after 30 seconds, which a `CREATE DATABASE` on a busy server can exceed. Nothing to change on upgrade.

## Problem
SQL Server runs `create database` and `drop database` with `CreateDropTimeoutSeconds = 600` (`src/DbLive.MSSQL/MsSqlDA.cs:605-610`, `:749`; comment "both wait for the files"). PostgreSQL sets no timeout (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:515-518` for `CREATE DATABASE`, `:620-631` for `revoke` and `drop`), so Npgsql's default of 30 seconds applies.

`CREATE DATABASE` before PostgreSQL 15 copies the template with checkpoints (strategy `FILE_COPY`) and can take longer than 30 seconds on a busy server; then `CreateDbIfNotExists` fails with a timeout. Not reproduced.

## Fix
The same 600 seconds on PostgreSQL.

## Done when
- [x] Fixed

## Result
- `PostgreSqlDA`: `CREATE DATABASE` (`CreateDBAsync`) and `drop database` (`DropDbAsync`) run with `CreateDropTimeoutSeconds = 600`, the value of SQL Server. `revoke connect` and `pg_terminate_backend` keep the default: they do not wait for files.
- No test of the timeout itself: a slow `CREATE DATABASE` cannot be made in a test. The PostgreSQL tests that create and drop their own databases (`SessionStateTests`, `CommandTimeoutTests`, `PostgreSqlContainerFixtureBuilderTests`) pass on PostgreSQL 18 in Docker.
- Docs: none; they do not name the timeouts of creating and dropping a database.
