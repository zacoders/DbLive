---
type: bug
priority: low
created: 2026-11-23
---
# PostgreSQL: `create database` and `drop database` keep the default timeout of 30 seconds

## Problem
SQL Server runs `create database` and `drop database` with `CreateDropTimeoutSeconds = 600` (`src/DbLive.MSSQL/MsSqlDA.cs:621-626`, `:765`; comment "both wait for the files"). PostgreSQL sets no timeout (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:509-512` for `CREATE DATABASE`, `:614-625` for `revoke` and `drop`), so Npgsql's default of 30 seconds applies.

`CREATE DATABASE` before PostgreSQL 15 copies the template with checkpoints (strategy `FILE_COPY`) and can take longer than 30 seconds on a busy server; then `CreateDbIfNotExists` fails with a timeout. Not reproduced.

## Fix
The same 600 seconds on PostgreSQL.

## Done when
- [ ] Fixed
