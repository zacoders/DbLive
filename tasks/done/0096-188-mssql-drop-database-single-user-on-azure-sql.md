---
type: bug
priority: medium
created: 2026-11-26
closed: 2026-11-26
---
# SQL Server: the drop of a database may fail on Azure SQL Database

## Release note
**The test fixtures drop their database on Azure SQL Database.** At the end of `DbLiveTestFixture` and of `DeploymentTest` (`dropDatabaseOnComplete: true`) the drop ran `alter database ... set single_user with rollback immediate`, which Azure SQL Database does not support, so the test database `DbLive--<guid>` stayed on the server. On Azure SQL Database the drop now runs `drop database` alone, which ends the sessions of the database itself. SQL Server and Azure SQL Managed Instance drop as before.

## Problem
`MsSqlDA.DropDbAsync` (`src/DbLive.MSSQL/MsSqlDA.cs`) always ran `alter database ... set single_user with rollback immediate; drop database ...;` in one batch. The syntax of `ALTER DATABASE ... SET` for Azure SQL Database in the docs of Microsoft has only `RESTRICTED_USER` and `MULTI_USER`, so the batch would fail with `Keyword or statement option 'single_user' is not supported ...`, and the drop in it would not run. Azure SQL Managed Instance takes `SINGLE_USER`. §2 of `docs/llms-full.txt` lists Azure SQL Database as supported. Read in the docs, not run on Azure.

A user reaches the drop only through the xUnit fixtures: the end of `DbLiveTestFixture` (`src/DbLive.xunit/SqlTest/DbLiveTestFixture.cs`) and of `DeploymentTest` (`src/DbLive.xunit/Deploy/DeploymentTest.cs`) with `dropDatabaseOnComplete: true`, with `MsSqlFixtureBuilder` against an Azure SQL Database server. The tests pass, the dispose fails, and `DbLive--<guid>` stays on the server, billed. `DeployAsync` drops too with `RecreateDatabase = true` (`src/DbLive/DbLive.cs`), but that option is internal: only the tests of this repository set it.

Rare: a database on Azure SQL Database takes long to create, and tests with a database per test class run there only if someone wants it.

The recipe of the docs for tests without `DbLive.xunit` (`docs/unit-testing/index.md`, §8.2 of `docs/llms-full.txt`) drops its database with `single_user` too, in the user's own SQL.

## Fix
On Azure SQL Database (`SERVERPROPERTY('EngineEdition') = 5`) the drop runs `drop database` alone, without `single_user` and without the kill of the sessions before it. Other editions keep the kill, `single_user with rollback immediate` and the drop.
- [x] What ends the sessions of the database on Azure SQL Database? `drop database` itself (the maintainer, 2026-11-26: never saw the drop fail there because of open connections). So nothing is killed before it: `KillSessionsAsync` runs in `master`, which on Azure SQL Database likely shows no session of another database.
- [x] The CI has no Azure server: a manual run on Azure SQL Database, written in Notes as verified? No (the maintainer, 2026-11-26): not run on Azure. The fix follows the syntax of `ALTER DATABASE ... SET` for Azure SQL Database in the docs of Microsoft.

## Docs to update
- `docs/llms-full.txt` §13.2 and `docs/unit-testing/index.md` (section "2. SQL tests: `[SqlFact]`"): the drop of the fixtures on Azure SQL Database
- `docs/llms-full.txt` §8.2 and `docs/unit-testing/index.md` (section "A test database without xUnit"): the recipe on Azure SQL Database leaves out the `alter database` line

## Done when
- [x] Fixed, with a unit test where the code branches on the edition

## Notes
- Found by the code review of 2026-11-26 (the branch as release 2.0).
- 2026-11-26, the maintainer: on Azure SQL Database just `drop database`; tests on Azure SQL Database are not a recommended setup (a database there takes long to create), but they should work for whoever runs them.
- `MsSqlDA.GetDropDbSql(quotedName, engineEdition)` builds the batch of the drop; `DropDbAsync` reads the edition in `master` after the check that the database exists, and skips `KillSessionsAsync` on Azure SQL Database.
- Not run on Azure SQL Database (the maintainer, 2026-11-26): the item "Verified on Azure SQL Database" left `## Done when`. The fix rests on the docs of Microsoft and on `MsSqlDADropDbSqlTests`.
- Tests: `MsSqlDADropDbSqlTests` checks the SQL of both branches without a server (edition 5; editions 2, 3, 4, 8). `MsSqlDADropDbTests` and `QuotedDatabaseNameTests` run the branch of SQL Server against the container, with the edition read from the server: pass.
- The docs: the fixtures and the recipe without `DbLive.xunit` say that Azure SQL Database drops without `single_user`. The code of the recipe stays as it is: it is for SQL Server, and the sentence under it says which line to leave out on Azure SQL Database.

## Result
`MsSqlDA.DropDbAsync` reads `SERVERPROPERTY('EngineEdition')`: on Azure SQL Database (5) it runs `drop database` alone and does not kill sessions before it; other editions keep the kill and `single_user with rollback immediate`. `MsSqlDADropDbSqlTests` covers the SQL of both branches; the branch of SQL Server runs against the container. The docs of the fixtures and of the recipe without `DbLive.xunit` name the difference.
