---
type: feature
priority: high
created: 2026-11-03
closed: 2026-11-03
---
# A clear error when the database does not exist

## Release note
**A missing database gets a clear error.** When the database of the connection string does not exist, `DeployAsync` (with `CreateDbIfNotExists` off, the default) and `RunAllTestsAsync()` fail before anything runs with `DatabaseNotFoundException` (namespace `DbLive.Adapter`), which names the database and the server. Before, SQL Server answered `Cannot open database "<name>" requested by the login. The login failed.` after about 10 seconds of connection retries, which reads like wrong credentials; now the error comes at once. A login without access to an existing database still gets that error from SQL Server. Nothing to change when you upgrade.

## Problem
Since 0096-34 a deployment does not create the database by default. When the database of the connection string does not exist, the user got the provider's connection error:

- SQL Server: `Cannot open database "<name>" requested by the login. The login failed. Login failed for user '<user>'.` (verified), after about 10 seconds of connection retries. "Login failed" points to the credentials, not to the database name.
- PostgreSQL: `3D000: database "<name>" does not exist`.

`RunAllTestsAsync()` against a missing database failed the same way, on its check for the DbLive tables.

## Decision
2026-11-03, maintainer: check that the database exists and throw a clear error.

## Where
- `src/DbLive/DbLive.cs`: database creation before the self-deployment
- `src/DbLive/Testing/DbLiveTester.cs`: the checks before the tests
- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`

## Fix
- [x] `IDbLiveDA.CheckDatabaseExistsAsync()` throws `DatabaseNotFoundException` (namespace `DbLive.Adapter`, with `DatabaseName` and `Server`) when the server confirms that the database does not exist.
- [x] SQL Server: open a connection to the database; on error 4060 ask `master`. Error 4060 is also the error of a login without access to an existing database, so only a `master` that shows no such database (and lets the login see all databases) means "does not exist".
- [x] PostgreSQL: open a connection; SQLSTATE `3D000` (`invalid_catalog_name`) means the database does not exist. A login without access gets another error (`42501`).
- [x] `DeployAsync` calls it when `CreateDbIfNotExists` is off, before the self-deployment; `RunAllTestsAsync()` before its other checks.

## Docs to update
- `docs/llms-full.txt` §3.3, §8.2, §11.3, §11.4, §12, §17
- `docs/deployment/index.md` (deployment steps)
- `docs/unit-testing/index.md` ("Running all tests without xUnit": the checks)

## Done when
- [x] Implemented, with unit tests
- [x] Integration tests on both providers: missing database (deployment and tests), and on SQL Server a login without access to an existing database
- [x] Docs updated

## Notes
- Verified (integration tests in Docker): SQL Server, missing database: `DatabaseNotFoundException` with the `SqlException` 4060 as the inner exception, in 0.1 to 0.7 seconds instead of 10. SQL Server, a login that exists and can connect to `master` but has no user in the existing database: `SqlException` 4060 from the self-deployment, after 10 seconds, as before. PostgreSQL, missing database: `DatabaseNotFoundException` with the `PostgresException` `3D000` as the inner exception.
- Verified: the 10 seconds are SqlClient's connection retry (`ConnectRetryCount`, `ConnectRetryInterval` 10 s by default; SqlClient treats 4060 as transient). The connection of the check has `ConnectRetryCount = 0`, and then the error comes at once.
- The check throws only for a database that is confirmed missing; every other outcome, also any other connection error, is left to the next connection, which uses the retry settings of the connection string. So transient errors (a database on Azure SQL coming online or failing over) are still retried. Not tested on Azure SQL.
- `master` can tell only when the login can connect to it and sees all databases: `sys.databases` shows other databases only with `VIEW ANY DATABASE` (granted to `public` by default, can be revoked). Without it the check cannot tell and leaves the 4060 error to the self-deployment.
- Rejected: asking `master` before connecting (needs access to `master` on every deployment, and without `VIEW ANY DATABASE` it would call an existing database missing); treating every 4060 as a missing database (also the error of a login without access).
- Not checked by `RunTestAsync` (`[SqlFact]`) and the `DbLive.xunit` fixtures: they create their database.
- The SQL Server test with a login without access clears the connection pools before `drop login`: the login's pooled connection to `master` blocks it.

## Result
- `DatabaseNotFoundException` (`DbLive.Adapter`): "Database '<name>' does not exist on server '<server>'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true."
- `IDbLiveDA.CheckDatabaseExistsAsync()`, implemented by `MsSqlDA` (connection without retries, then `master` on error 4060) and `PostgreSqlDA` (SQLSTATE `3D000`). `DbLive.DeployAsync` calls it when it does not create the database; `DbLiveTester.RunAllTestsAsync()` calls it first.
- Unit tests: `DbLiveTests` (the check runs only without `CreateDbIfNotExists`; a missing database stops the deployment before the self-deployment), `DbLiveTesterTests` (the check comes first; a missing database runs no check and no test). Integration tests: `MissingDatabaseTests` in `DbLive.MSSQL.Tests` (deployment, tests, login without access) and `DbLive.PostgreSQL.Tests` (deployment, tests).
- `dotnet test src/DbLive.sln --filter "Category!=LocalOnly"`: 494 passed, 0 failed (DbLive.Tests 388, DbLive.MSSQL.Tests 49, DbLive.PostgreSQL.Tests 36, DemoMSSQL.Tests 12, Demo.PostgreSQL.Chinook.Tests 5, AdventureWorks.Tests 4), with SQL Server and PostgreSQL in Docker. `mkdocs build --strict` passes.
