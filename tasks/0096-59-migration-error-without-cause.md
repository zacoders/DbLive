---
type: bug
priority: medium
created: 2026-11-08
---
# A failed migration can lose its cause

## Problem
When a migration script fails with `DbLiveSqlException`, `MigrationItemDeployer` throws `MigrationDeploymentException` with the file name and `ex.SqlError` in the message, and without an inner exception. `SqlError` is filled only for an error of the server:

- PostgreSQL: `PostgreSqlDA.GetSqlError` returns text only when the exception itself is `PostgresException`. For any other `NpgsqlException` (a command timeout, a lost connection) it returns `""`.
- SQL Server: `GetSqlServerError` looks for a `SqlException` in the chain of inner exceptions, and returns `""` without one (for example an `InvalidOperationException` of the connection pool).

Then the message is `Error deploying Migrations/005.migration.sql file.` followed by a line with only `.`. The cause is only in the log (`_logger.Error(ex, ...)`), and only if the log is written somewhere. `docs/llms-full.txt` §17 says that a long statement "usually fails first with the provider's timeout error, as `MigrationDeploymentException`", and that the message contains the SQL error.

Example: a long migration on PostgreSQL exceeds the `Timeout` of its item settings. The exception of a Npgsql command timeout is not `PostgresException` (from the code; the exact exception type of Npgsql 10 not verified), so the message has no reason.

Found by the code review of 2026-11-08 (finding 2).

## Where
- `src/DbLive/Deployers/Migrations/MigrationItemDeployer.cs:84-97`
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:613` (`GetSqlError`), `src/DbLive.MSSQL/MsSqlDA.cs` (`GetSqlServerError`)

## Fix
Proposal: pass `ex` as the inner exception of `MigrationDeploymentException`, and put `ex.Message` in the message when `SqlError` is empty. Check the other places that build a message from `SqlError` the same way.

- [ ] Inner exception for every `DbLiveSqlException`, or only when `SqlError` is empty? The docs now say on purpose that there is none for a SQL error.

## Docs to update
- `docs/llms-full.txt` §5.3, §17 (`MigrationDeploymentException`, `TransactionAbortedException`)
- `docs/deployment/migrations.md` ("Errors")

## Done when
- [ ] Reproduced: a Npgsql command timeout in a migration, and the message of the exception
- [ ] Fixed, with a unit test (`DbLiveSqlException` with an empty `SqlError`) and an integration test of the timeout on PostgreSQL
- [ ] Docs updated
