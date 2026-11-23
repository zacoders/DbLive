---
type: bug
priority: low
created: 2026-11-23
closed: 2026-11-23
---
# The database name goes into `create database` and `drop database` without escaping

## Release note
A database whose name has a `]` (SQL Server) or a `"` (PostgreSQL) can be created and dropped. SQL Server creates it (`CreateDbIfNotExists`, `RecreateDatabase`), and both providers drop it (`RecreateDatabase`, the test fixtures and `[SqlDeployFact]`). Before, these statements failed with a syntax error, because the name went into the SQL without escaping. Nothing to change on upgrade.

## Problem
- SQL Server: `create database [{databaseToCreate}];` (`src/DbLive.MSSQL/MsSqlDA.cs:605`), `alter database [{databaseToDrop}] ...; drop database [{databaseToDrop}];` (`:746-747`).
- PostgreSQL: `revoke connect on database "{dbName}"` and `drop database "{dbName}"` (`src/DbLive.PostgreSQL/PostgreSqlDA.cs:621`, `:630`), while `CreateDBAsync` quotes the name with `NpgsqlCommandBuilder.QuoteIdentifier` (`:516`).

A valid database name with `]` (SQL Server) or `"` (PostgreSQL) breaks `CreateDbIfNotExists` (`docs/llms-full.txt` §11.1: "The database name in it is created when `CreateDbIfNotExists` is true") and the drop of the fixtures. The name comes from the connection string, which is trusted configuration, so this is not a security issue in practice.

## Fix
SQL Server: escape `]` as `]]` (or `quotename` in dynamic SQL). PostgreSQL: `QuoteIdentifier` in `DropDbAsync` too.

## Done when
- [x] Fixed, with an integration test per provider: create and drop a database whose name has `]` / `"`

## Notes
- Verified: without the fix the new tests fail. SQL Server, name `DbLive]['-<guid>`: `Incorrect syntax near ''-<guid>'` at `create database`. PostgreSQL, name `dblive"'-<guid>`: `42601: unterminated quoted string` at `revoke connect` of the drop; `CREATE DATABASE` already quoted the name.
- The quoting of the driver on both providers: `SqlCommandBuilder.QuoteIdentifier` (`[...]`, `]` doubled) and `NpgsqlCommandBuilder.QuoteIdentifier` (`"..."`, `"` doubled), one helper `QuoteDbName` in each DA. The rest of the SQL that takes the name (`sys.databases`, `db_id`, `pg_database`, `pg_stat_activity`) takes it as a parameter.
- The test names also have a `'`, so that a later string literal with the name fails the tests too. SQL Server has no `"` in the name: the data files are named after the database, and `"` is not valid in a file name on Windows.
- Out of scope: the demo script `src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/AfterDeploy/query-store.sql` builds `ALTER DATABASE [` + `db_name()` + `]` without `quotename`. It is a demo with a known database name, not DbLive code.

## Result
- `MsSqlDA`: `create database`, `alter database ... set single_user` and `drop database` take the name from `QuoteDbName` (`SqlCommandBuilder.QuoteIdentifier`).
- `PostgreSqlDA`: `CREATE DATABASE`, `revoke connect` and `drop database` take the name from `QuoteDbName` (`NpgsqlCommandBuilder.QuoteIdentifier`); before, only `CREATE DATABASE` quoted it.
- Tests: `QuotedDatabaseNameTests` in `DbLive.MSSQL.Tests` and `DbLive.PostgreSQL.Tests` deploy with `CreateDbIfNotExists = true` into a database with such a name, then drop it with `DropDbAsync`. `PostgreSqlDbTests` takes a fixed `dbName`, as `SqlServerDbTests` does. All tests pass: `DbLive.MSSQL.Tests` 159, `DbLive.PostgreSQL.Tests` 90, `DbLive.Tests` 583.
- Docs: none; they set no limit on the database name.
