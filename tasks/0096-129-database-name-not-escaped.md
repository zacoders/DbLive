---
type: bug
priority: low
created: 2026-11-23
---
# The database name goes into `create database` and `drop database` without escaping

## Problem
- SQL Server: `create database [{databaseToCreate}];` (`src/DbLive.MSSQL/MsSqlDA.cs:621`), `alter database [{databaseToDrop}] ...; drop database [{databaseToDrop}];` (`:762-763`).
- PostgreSQL: `revoke connect on database "{dbName}"` and `drop database "{dbName}"` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:615`, `:624`), while `CreateDBAsync` quotes the name with `NpgsqlCommandBuilder.QuoteIdentifier` (`:510`).

A valid database name with `]` (SQL Server) or `"` (PostgreSQL) breaks `CreateDbIfNotExists` (`docs/llms-full.txt` §11.1: "The database name in it is created when `CreateDbIfNotExists` is true") and the drop of the fixtures. The name comes from the connection string, which is trusted configuration, so this is not a security issue in practice.

## Fix
SQL Server: escape `]` as `]]` (or `quotename` in dynamic SQL). PostgreSQL: `QuoteIdentifier` in `DropDbAsync` too.

## Done when
- [ ] Fixed, with an integration test per provider: create and drop a database whose name has `]` / `"`
