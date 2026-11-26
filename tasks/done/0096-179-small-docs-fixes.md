---
type: docs
priority: low
created: 2026-11-25
closed: 2026-11-25
---
# Small docs fixes from the review of 2026-11-25

## Release note
None. Corrections of the XML docs of `IDbLive`, `CodeDeploymentException` and the container fixture builders, of the docs pages and of the demo settings files; DbLive behaves as before.

## Problem
Statements of the docs, XML docs and demos that the code does not match, each small.

1. **A PostgreSQL connection back in the pool "is reset".** §10 (line 736), §16 (line 1078: "Reset: Npgsql runs `discard all`") and §11.1. True while `No Reset On Close` is off, the default. With `No Reset On Close=true` a `SET search_path` or `SET ROLE` of a script without a transaction stays on the pooled connection, for the next scripts and the application. Add the condition. Read in the code of Npgsql, not run.
2. **Fill factor on PostgreSQL.** §13.3 (line 996), "Not compared: ... physical options (fill factor, ...)". The PostgreSQL snapshot reads an index with `pg_get_indexdef` (`src/DbLive.PostgreSQL/PostgreSqlSchemaSnapshot.cs:141`), which prints `WITH (fillfactor='80')` and the other storage parameters, so they are compared (not those of a primary key or unique constraint). Verified on PostgreSQL 18. Say "on SQL Server", or list the storage parameters of a PostgreSQL index as compared.
3. **Keep-alive and `idle_session_timeout`.** §12.1 (line 893): the keep-alive of Npgsql is "a message of the protocol, so `idle_session_timeout` of the server does not end the session either". Only when `idle_session_timeout` is longer than the 30 seconds of the keep-alive; the experiment of 0096-97 shows a shorter one ending the session.
4. **Online index on SQL Server.** §18.7 (line 1196) "SQL Server: `with (online = on)`", `docs/deployment/migrations.md:89`, §9.3 (line 704: `None` "needed for online/concurrent index builds"), §16 (line 1069: "online index with long locks" among the statements that cannot run in a transaction). `ONLINE = ON` needs Enterprise or Developer edition or Azure SQL; Standard fails with Msg 1712 (not run). And `create index ... with (online = on)` does run in a transaction (verified on SQL Server 2025: `@@trancount` 1, the index created): `None` is for the locks a transaction holds to its commit, not because the statement cannot run there. State the edition, reword §9.3 and §16.
5. **XML doc of `CodeDeploymentException`** (`src/DbLive/Deployers/Code/CodeDeploymentException.cs:8`): "No row of the file is written to the `dblive` tables: the row keeps the last success." Since 0096-142 a code file's `applied` row becomes `applying` before it runs and stays so after a failure, and the next deployment runs the file whatever its content (§6, §15). Right for a breaking script of the Code folder only.
6. **XML doc of the container builders** (`MsSqlContainerFixtureBuilder.cs:9-10`, `PostgreSqlContainerFixtureBuilder.cs`): "Testcontainers removes the container when the test process ends." §11.5, `docs/unit-testing/index.md` and both READMEs add that with `TESTCONTAINERS_RYUK_DISABLED=true` nothing removes it (0096-160, item 5, left out the XML docs).
7. **XML doc of `IDbLive.DeployAsync`** (`src/DbLive/IDbLive.cs`): the exceptions lack `NotSupportedTransactionIsolationLevelException` (project load, §9.3, §12 step 1), which `IDbLiveTester.RunAllTestsAsync` lists; the errors of the project load (`InvalidProjectSettingsException`, `FilesOutsideProjectFoldersException`, ...) are named only in general words.
8. **§11 namespaces** (`docs/llms-full.txt:756`): "the exceptions of Section 17 but three". §17 lists .NET and SqlClient exceptions too (`DirectoryNotFoundException`, `NotSupportedException`, `KeyNotFoundException`, `SqlException` 4060). "DbLive's exceptions of Section 17 but three".
9. **`Development.md`**: "Every package gets a symbols package" (line 106) and the check after a release "Every package on nuget.org has its symbols package" (line 114). `DbLive.Templates.csproj:26` sets `IncludeSymbols=false` (0096-30): six `.snupkg` for seven packages. The notes of 0096-93 say to change the description of the GitHub repository ("safe rollback") when 2.0 is released; "After a release" does not list it.
10. **Demo `settings.json` comments**: `src/Demo/DemoMSSQL/Scripts/settings.json:11` and `src/Demo/AdventureWorks/AdventureWorks.Database/Scripts/settings.json:11` say "none - no transactions". With `None` each code file, each SQL test and the self-deployment still run in a transaction (§10); 0096-149 corrected the same words in `configuration.md`.
11. **Chinook index migration**: `src/Demo/Demo.PostgreSQL.Chinook/Scripts/Migrations/20260208142812.migration.settings.json` has `"TransactionWrapLevel": "none", // no transaction for online index creation`, and `20260208142812.indexes.sql` runs three plain `create index` in one file (the `concurrently` lines are commented out). On PostgreSQL a file of several statements is one implicit transaction (§16) and a plain `create index` blocks writes: nothing is online, nothing runs without a transaction. `docs/examples/index.md` offers the demos as templates. Correct the comment (or the settings); the migration itself is applied in the demo databases, so it stays.

## Docs to update
- `docs/llms-full.txt` §9.3, §10, §11, §11.1, §12.1, §13.3, §16, §18.7, and the pages that repeat them (`docs/deployment/migrations.md`, `docs/deployment/index.md`, `docs/unit-testing/deployment-tests.md`)
- XML docs of `CodeDeploymentException`, `MsSqlContainerFixtureBuilder`, `PostgreSqlContainerFixtureBuilder`, `IDbLive`
- `Development.md` ("Assembly version and symbols", "After a release")
- the demo files of 10 and 11

## Done when
- [x] 1–11 fixed, `mkdocs build --strict` passes

## Notes
- Found by the code review of 2026-11-25.
- `docs/unit-testing/deployment-tests.md` does not name the physical options, so item 2 needs no change there.
- 2026-11-25, the maintainer decided: index builds, online or not, run without a transaction (`None`). In a transaction the locks of a build are held to the commit, without one they are released when the build ends, and the database stays available (24/7, database as a service). So item 11 keeps `"TransactionWrapLevel": "None"` in the Chinook settings file and corrects only its comment: on PostgreSQL the three `create index` of the file are one implicit transaction all the same, and the comment says to give each index a version of its own. The `.sql` file is applied in the demo databases and stays; settings items are never checksum-validated (§5.4).
- Left as it is: `docs/database-as-service.md:101` lists "online index builds" among the statements that cannot run in a transaction (item 4). The page is the maintainer's own text.

## Result
- `docs/llms-full.txt`: §10, §11.1 and §16 name `No Reset On Close=true`, with which a `SET`, a temporary table and the isolation level set for a script without a transaction stay on the pooled PostgreSQL connection (§11.1: leave it out of the connection string of DbLive); §13.3 says that the physical options are not compared on SQL Server, and that the storage parameters of a PostgreSQL index are compared, not those of the index of a primary key or unique constraint; §12.1: an `idle_session_timeout` shorter than the keep-alive ends the session of the lock; §9.3 and §16: an index build, also an online one, runs in a transaction, and gets `None` so that its locks are released when it ends; §18.7: `with (online = on)` needs Enterprise or Developer edition or Azure SQL; §11: "DbLive's exceptions of Section 17".
- `docs/deployment/index.md` (`No Reset On Close`), `docs/deployment/migrations.md` (the edition of an online index build).
- XML docs: `CodeDeploymentException` (the `applying` row of a code file), both container builders (`TESTCONTAINERS_RYUK_DISABLED`), `IDbLive.DeployAsync` (`NotSupportedTransactionIsolationLevelException`, `InvalidProjectSettingsException` and the errors of the project files). A comment of `PostgreSqlDA.SetTransactionIsolationLevelAsync`.
- `Development.md`: no symbols package for `DbLive.Templates`; "After a release" lists the description of the GitHub repository after 2.0. The release note of 0096-18 says "every package with an assembly".
- Demos: the comments of `TransactionWrapLevel` in the `settings.json` of DemoMSSQL and AdventureWorks; the comment of the Chinook `20260208142812.migration.settings.json`, which keeps `None` (decision above).
- `mkdocs build --strict` passes; `DbLive`, `DbLive.Testcontainers.MSSQL` and `DbLive.Testcontainers.PostgreSQL` build without warnings.
