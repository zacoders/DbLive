---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
breaking: true
---
# SQL Server: every deployment fails with `Pooling=False` in the connection string

## Release note
`DeployAsync` and `RunAllTestsAsync` check the connection string at their start, before they connect: `Pooling=False` on SQL Server and `Enlist=False` on either provider fail with a `NotSupportedException` that names the setting. With `Pooling=False` on SQL Server every deployment failed before as well, with "Implicit distributed transactions have not been enabled", which did not name the setting. With `Enlist=False` DbLive 1.5 ran the scripts outside its transactions: a failed migration was not rolled back, and a SQL test kept its changes. Remove the setting from the connection string. PostgreSQL keeps working with `Pooling=false`.

## Problem
DbLive opens a new `SqlConnection` for each call of its data access, also several in one `TransactionScope`: the self-deployment (`DbLiveSelfDeployer.cs:34-67`: `DbLiveInstalledAsync`, `GetMinDbLiveLibVersionAsync`, the self-migrations, ...), a migration with its row, the deployment transaction. With pooling, the second connection of a scope gets the same session back from the pool of the transaction. With `Pooling=False` it is a new session, the transaction is promoted to a distributed one, and .NET fails it: `NotSupportedException: Implicit distributed transactions have not been enabled`. So with `...;Pooling=False` the first deployment fails in the self-deployment, before any script of the project. Verified: 0096-69 (Notes), and again by the review on 2026-11-24 with SqlClient 6.1.4 on SQL Server 2017 and 2025. PostgreSQL is not affected: with `Pooling=false` Npgsql keeps the enlisted session for the transaction (verified on PostgreSQL 13 the same day).

`docs/llms-full.txt` §11.1 (`SetDbConnection`, line 769) puts no requirement on the connection string, and §17 has no row for the error; §8.2 (line 512) names the error only for `ClearAllPools`.

Related: `Enlist=False` on either provider takes the connections out of the transactions of DbLive. The transaction check reads the ID of the transaction in autocommit mode then, and every script in a transaction fails with "The script ended the transaction that DbLive runs it in" (from the code, not run). An operator mistake: a line in the docs is enough.

## Where
- `src/DbLive.MSSQL/MsSqlDA.cs:57` (`GetConnection`: the connection string as it is)

## Fix
- [x] Force `Pooling=true` (and `Enlist=true`) in `GetConnection`, or document both keys? Forcing changes the pool key only when the user set them, so the recipe of 0096-81 (`ClearPool(new SqlConnection(connectionString))`) keeps working for everyone else.
  Decided by the maintainer on 2026-11-24: not forced. Both keys are documented, and `DeployAsync` and `RunAllTestsAsync` fail at their start with an error that names the setting.

## Docs to update
- `docs/llms-full.txt` §11.1 (`SetDbConnection`), §8.2 (the checks before the first test), §11.4, §12 (before step 1), §17 (rows for both `NotSupportedException` messages, and for "Implicit distributed transactions have not been enabled" after `ClearAllPools`)
- `docs/deployment/index.md` ("A deployer")
- `docs/unit-testing/index.md` (the checks of `RunAllTestsAsync`)

## Done when
- [x] Fixed or documented, as decided

## Notes
- Found by the code review of 2026-11-24.
- Verified on 2026-11-24 with a deployment of one migration, before the check (the test containers, SQL Server 2025 and PostgreSQL 18):
  - SQL Server, `Pooling=False`: `DbLiveSqlException: Implicit distributed transactions have not been enabled`, no table left in the database.
  - SQL Server and PostgreSQL, `Enlist=False`: `DbLiveTransactionEndedException` ("Remove COMMIT and ROLLBACK from the script") on the first self-migration, no table left. So `Enlist=False` is not only a docs matter: its message sends the user to a COMMIT that no script has.
  - PostgreSQL, `Pooling=false`: the deployment succeeds. Only `Enlist` is checked there.
- Not breaking: no setup that worked fails now. A deployment opens several connections in one transaction from the self-deployment on, and every SQL test at least two (the test, then `IsTransactionUsableAsync`, `UnitTestItemRunner`), so with either setting every deployment and every SQL test failed already (from the code for the tests).
  2026-11-25 (0096-180): true of 2.0 before this task, not of 1.5.0, which the release is compared with. DbLive 1.5 has no transaction check, so with `Enlist=False` it deployed without rollback and its SQL tests passed and kept their changes (from the 1.5.0 code, not run). Such a setup fails now: `breaking: true`.
- Not forced in the connection string: it would silently override a setting the user chose, against the decision of 0096-148 not to rewrite the connection string; and a user who turned pooling off so that no session stays after the deployment would get pooled sessions in a pool that `ClearPool` with their own string does not reach. A builder that rewrites the string also normalizes it, so it could only be rewritten when one of the keys is off.
- The check is a call at the start of `DeployAsync` and `RunAllTestsAsync` (`IDbLiveDA.CheckConnectionString`), not a lazy one in `GetConnection`: the script paths of both data accesses wrap any exception in `DbLiveSqlException`, so the type of the error would depend on which call connects first. `RunTestAsync` does not check, as it checks nothing else (Section 11.4): the `[SqlFact]` fixture has just deployed the database, and that deployment checked the string.
- An invalid connection string (an unknown keyword) now fails at the start of the deployment with the `ArgumentException` of the provider, before the project is loaded; before, it failed with the same exception after the project was loaded.

## Result
- `IDbLiveDA.CheckConnectionString()`: SQL Server rejects `Pooling=False` and `Enlist=False`, PostgreSQL `Enlist=False`, with a `NotSupportedException` from `ConnectionStringNotSupported` (`src/DbLive/Adapter`). Called first in `DbLive.DeployAsync` and `DbLiveTester.RunAllTestsAsync`, without connecting.
- Tests: `ConnectionStringSettingsTests` in `DbLive.MSSQL.Tests` (a deployment without pooling and one without enlist fail with the message and leave no table; `RunAllTestsAsync` without pooling fails) and in `DbLive.PostgreSQL.Tests` (the same for `Enlist=false`; a deployment and the SQL tests with `Pooling=false` succeed).
- Docs: the requirement in `SetDbConnection` (§11.1), §8.2, §11.4, §12, three rows in §17; `docs/deployment/index.md` and `docs/unit-testing/index.md`.
