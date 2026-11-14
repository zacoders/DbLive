---
type: bug
priority: medium
created: 2026-11-08
closed: 2026-11-08
---
# A failed migration can lose its cause

## Release note
A command timeout on PostgreSQL is reported with its cause. Npgsql reports it as `Exception while reading from stream` with the cause in the inner exception, and DbLive showed only that line or nothing: a migration that ran longer than its `Timeout` failed with `Error deploying <file> file.` and no reason, and a SQL test longer than `UnitTestItemTimeout` failed with `Exception while reading from stream`. The message of `DbLiveSqlException` on PostgreSQL now holds the messages of the whole exception chain, `Timeout during reading attempt` for a timeout; a lost connection is reported the same way. `MigrationDeploymentException` now has the `DbLiveSqlException` as its inner exception on both providers. Nothing to change on upgrade.

## Problem
When a migration script fails with `DbLiveSqlException`, `MigrationItemDeployer` throws `MigrationDeploymentException` with the file name and `ex.SqlError` in the message, and without an inner exception. `SqlError` is filled only for an error of the server:

- PostgreSQL: `PostgreSqlDA.GetSqlError` returns text only when the exception itself is `PostgresException`. For any other `NpgsqlException` (a command timeout, a lost connection) it returns `""`.
- SQL Server: `GetSqlServerError` looks for a `SqlException` in the chain of inner exceptions, and returns `""` without one (for example an `InvalidOperationException` of the connection pool).

Then the message is `Error deploying Migrations/005.migration.sql file.` followed by a line with only `.`. The cause is only in the log (`_logger.Error(ex, ...)`), and only if the log is written somewhere. `docs/llms-full.txt` §17 says that a long statement "usually fails first with the provider's timeout error, as `MigrationDeploymentException`", and that the message contains the SQL error.

Example: a long migration on PostgreSQL exceeds the `Timeout` of its item settings. The exception of a Npgsql command timeout is not `PostgresException`, so the message has no reason (verified, see Notes).

Found by the code review of 2026-11-08 (finding 2).

## Where
- `src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:84-97`
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:613` (`GetSqlError`), `src/DbLive.MSSQL/MsSqlDA.cs` (`GetSqlServerError`)

## Fix
Pass `ex` as the inner exception of `MigrationDeploymentException`, and put the message of the `DbLiveSqlException` in the message when `SqlError` is empty. `PostgreSqlDA` builds that message from the messages of the exception chain, so every place that shows `ex.Message` gets the cause. Check the other places that build a message from `SqlError` the same way.

- [x] Inner exception for every `DbLiveSqlException`, or only when `SqlError` is empty? The docs now say on purpose that there is none for a SQL error.
  Maintainer, 2026-11-08: for every `DbLiveSqlException`.
- [x] Join the messages of the chain in `MigrationItemDeployer` only, or in `PostgreSqlDA` for every caller?
  Maintainer, 2026-11-08: in `PostgreSqlDA`. SQL tests show only `ex.Message` and lost the cause the same way.

## Docs to update
- `docs/llms-full.txt` §5.3, §8.2, §11.4, §17 (`MigrationDeploymentException`, `TransactionAbortedException`)
- `docs/deployment/migrations.md` ("Errors")
- `docs/unit-testing/index.md` ("Isolation and parallelism")

## Done when
- [x] Reproduced: a Npgsql command timeout in a migration, and the message of the exception
- [x] Fixed, with a unit test (`DbLiveSqlException` with an empty `SqlError`) and an integration test of the timeout on PostgreSQL
- [x] SQL tests on PostgreSQL report the cause of a timeout, with an integration test
- [x] Docs updated

## Notes
- Reproduced (verified, Npgsql 10.0.1): `select pg_sleep(10);` with `"Timeout": "00:00:01"` in the item settings failed with the message `Error deploying Migrations/001.migration.sql file.` and a line `.`. Npgsql throws `NpgsqlException("Exception while reading from stream")` with an inner `TimeoutException("Timeout during reading attempt")`; it cancels the query on the server, but does not throw the `PostgresException` 57014 of the cancellation.
- With the default `TransactionWrapLevel: Migration` and the same 1 second for the transaction and the command, the command timeout came first and the message was the same (verified once, not a test: which timeout fires first is a race). The integration tests use `TransactionWrapLevel: None`, so only the command timeout applies.
- SQL Server: a command timeout is a `SqlException` with number -2 ("Execution Timeout Expired") inside the exception of `ServerConnection`, and `GetSqlServerError` finds it, so the message had the reason already (verified by the new test). An empty `SqlError` there needs an error without a `SqlException`, such as an exhausted connection pool.
- No other place builds a message from `SqlError`. Code files (`CodeItemDeployer`, `CodeBreakingDeployer`) pass the exception as the inner exception and store `ex.ToString()`; `FolderDeployer` lets `DbLiveSqlException` through (its missing file name is 0096-63). They kept the cause in the chain, but `ex.Message` alone was `Exception while reading from stream` on PostgreSQL.
- SQL tests lost the cause too: `UnitTestItemRunner` puts only `ex.Message` in `ErrorMessage` and `Output`, which Test Explorer and `dblive.unit_test_run` show. A test with `select pg_sleep(10);` and `"UnitTestItemTimeout": "00:00:01"` failed with `ErrorMessage` `Exception while reading from stream` (verified: the new test fails with the old message of `PostgreSqlDA`).
- The `.` after `{ex.SqlError}` is gone: the error of SQL Server ends with a new line, so the dot stood on a line of its own even for a SQL error.

## Result
- `PostgreSqlDA.GetMessage`: the message of `DbLiveSqlException` is the message of a `PostgresException` as before; for any other exception it is the distinct messages of the chain, one per line (`Exception while reading from stream` and `Timeout during reading attempt` for a command timeout). Used by `ExecuteNonQueryAsync` and `ExecuteQueryMultipleAsync`. `MsSqlDA` needs no change: its message is the `SqlException`'s (`Execution Timeout Expired`) or that of the exception itself.
- `MigrationItemDeployer` passes the `DbLiveSqlException` as the inner exception of `MigrationDeploymentException`. The message is the file and `SqlError`, or `ex.Message` when `SqlError` is empty. The log line gets the same text.
- Tests: unit tests `PostgreSqlDAMessageTests` (`InternalsVisibleTo` `DbLive.Tests` in `DbLive.PostgreSQL`) and `MigrationItemDeployerTests` (the message and the inner exception with a `SqlError`, and with an empty one). Integration tests `CommandTimeoutTests` of both providers: a migration and a SQL test over their timeout (`Timeout during reading attempt` and `TimeoutException` on PostgreSQL, `Execution Timeout Expired` and `SqlException` -2 on SQL Server). Both PostgreSQL tests failed before the fix.
- Docs: `docs/llms-full.txt` §5.3, §8.2, §11.4, §17; `docs/deployment/migrations.md`; `docs/unit-testing/index.md`.
