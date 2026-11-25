---
type: bug
priority: medium
created: 2026-11-26
---
# SQL Server: the drop of a database may fail on Azure SQL Database

## Problem
`MsSqlDA.DropDbAsync` (`src/DbLive.MSSQL/MsSqlDA.cs:830-866`) always runs `alter database ... set single_user with rollback immediate; drop database ...;` (line 862). The syntax of `ALTER DATABASE ... SET` for Azure SQL Database in the docs of Microsoft has only `RESTRICTED_USER` and `MULTI_USER`, so the drop would fail with `Keyword or statement option 'single_user' is not supported ...`. Azure SQL Managed Instance takes `SINGLE_USER`. §2 (line 76) lists Azure SQL Database as supported. Read in the docs, not run on Azure: verify first.

The drop runs:
- at the end of the xUnit fixtures (`src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs:91`) and of `DeploymentTest` with `dropDatabaseOnComplete: true` (`src/DbLive.xunit/Deploy/DeploymentTest.cs:104`), with `MsSqlFixtureBuilder` against a server you run: the tests pass, the dispose fails, and `DbLive--<guid>` stays on the server, billed;
- in `DeployAsync` with `RecreateDatabase = true` (`src/DbLive/DbLive.cs:31`).

## Fix
Find the edition (`SERVERPROPERTY('EngineEdition') = 5` is Azure SQL Database) and leave `single_user` out there.
- [ ] What ends the sessions of the database on Azure SQL Database? `KillSessionsAsync` (line 878) runs in `master`, where Azure SQL Database may not show the sessions of another database; `restricted_user with rollback immediate` keeps the sessions of `db_owner`, which DbLive's own login usually is; `DROP DATABASE` may end them itself or fail while one is open. To find out on Azure.
- [ ] The CI has no Azure server: a manual run on Azure SQL Database, written in Notes as verified?

## Docs to update
- `docs/llms-full.txt` §11.5 (the drop of the fixture builders), only if the behavior on Azure differs

## Done when
- [ ] Verified on Azure SQL Database: the drop fails today, and works after the fix
- [ ] Fixed, with a unit test where the code branches on the edition

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
