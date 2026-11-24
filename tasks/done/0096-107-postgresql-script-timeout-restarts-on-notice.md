---
type: bug
priority: medium
created: 2026-11-23
closed: 2026-11-24
breaking: true
---
# PostgreSQL: a script that sends notices runs past its timeout

## Release note
**PostgreSQL: the timeout of a script holds when the script sends notices.** DbLive limited a PostgreSQL script with the command timeout of Npgsql, which starts again on every notice of the server: a script with `raise notice` in a loop ran past `MigrationTimeout`, the `Timeout` of a migration, `CodeItemTimeout`, `BeforeDeployFolderTimeout`, `AfterDeployFolderTimeout` or `UnitTestItemTimeout` for as long as the notices came, and a notice of `drop ... if exists` gave the rest of the script its whole timeout again. Now DbLive cancels the script when its time runs out, and it fails with `ExecutionTimeoutException`. A script that ran longer than its timeout and deployed before fails now: raise its timeout (for one migration: `Timeout` in its item settings file) or split the file.

## Problem
`docs/llms-full.txt` §10 ("Script timeouts") and the `ExecutionTimeoutException` row of §17 say that `CodeItemTimeout`, `MigrationTimeout`, the folder timeouts, `UnitTestItemTimeout` and the item `Timeout` limit each script as a whole, the same on both providers.

On PostgreSQL DbLive sets only `NpgsqlCommand.CommandTimeout`. Npgsql restarts that timeout when a notice arrives from the server, so a script that sends notices can run much longer than its timeout. Verified on 2026-11-23 on PostgreSQL 18 with Npgsql 10.0.1, `CommandTimeout = 3`:

| Command | Result |
|---|---|
| `do $$ begin for i in 1..5 loop raise notice '%', i; perform pg_sleep(2); end loop; end $$;` | failed after 9.0 s ("Exception while reading from stream") |
| the same loop without `raise notice` | failed after 3.1 s |
| `select pg_sleep(2); select pg_sleep(2); select pg_sleep(2);` | failed after 3.1 s |

Notices are common on PostgreSQL: `drop ... if exists` of a missing object, `create ... if not exists` of an existing one, `raise notice` in a data migration that reports its progress. The test of 0096-84 (two `pg_sleep(2)`, timeout 3 s) sends no notice, so it does not catch this.

## Where
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs`: `ExecuteCommandAsync` (also the code file in its own transaction, through `ExecuteInOwnTransactionAsync`) and `QueryMultipleAsync`, the reader of the tests.

## Fix
Enforce the deadline in DbLive: a `CancellationTokenSource` with `CancelAfter(timeout)`, passed to `ExecuteNonQueryAsync` / `ExecuteReaderAsync`. Map the cancellation (Npgsql throws `OperationCanceledException` with a `57014` `PostgresException` inside) to `ExecutionTimeoutException`, as the command timeout is mapped now. Keep the exclusion for a transaction that System.Transactions aborted (`IsTransactionAborted`).

- [x] Keep `CommandTimeout` as well, as a second limit? No: `CommandTimeout = 0` (maintainer, 2026-11-24).

## Docs to update
- `docs/llms-full.txt` §10 ("Script timeouts": how PostgreSQL limits a script, the inner exception), §16 (a row "Timeout of a script"), §17 (the row of `ExecutionTimeoutException`)
- `docs/project/configuration.md` (section "Timeouts")

## Done when
- [x] Fixed, with a PostgreSQL integration test: a script that sends a notice every second fails at its timeout of 3 s with `ExecutionTimeoutException`
- [x] The reader of the tests too: a SQL test that sends notices, and the statements after the first result set of a test
- [x] Docs updated

## Notes
- The impact without the fix, from the code: a migration in its own transaction (the default) stopped in time anyway, because the `TransactionScope` has the same timeout and Npgsql cancels the command when it aborts the transaction; only the error named the transaction instead of the script. A test stopped at twice `UnitTestItemTimeout`. Without a stop: code files (a raw `begin`/`commit`, no `TransactionScope`), migrations and breaking scripts with `None`, BeforeDeploy and AfterDeploy, and with `Deployment` everything up to `DeploymentTimeout`. One early notice gave the script its timeout again once; a notice more often than the timeout made it unlimited.
- The tests send a notice every second, not every 2 s: with 2 s the old code also failed with `ExecutionTimeoutException`, only later (9 s in the table above), so the test would need a measure of the time, which the load of the parallel tests makes unreliable. With 1 s the old code ran the script of 5 s to its end (verified), and the test fails on it without a clock.
- `CommandTimeout = 0` is set explicitly: without it the command timeout of the connection string applies, 30 seconds by default.
- The token goes to every call of the reader (`ExecuteReaderAsync`, `NextResultAsync`, `ReadAsync`, `GetColumnSchemaAsync`): the server runs the statements after the first result set while the reader moves on.
- The cancel is the timeout of the script only when DbLive's token was cancelled, not by the shape of the exception. Npgsql throws `OperationCanceledException` with `PostgresException` 57014 inside when the server cancelled the statement (verified).
- `CancelAfter` takes at most `uint.MaxValue - 1` milliseconds, about 49.7 days, and throws on more; `"Timeout": "60"` of the item settings is 60 days. A longer time left is no limit (`ScriptTimeout.MaxCancelDelay`): no deployment reaches it.
- The cancel of the running command at the abort of the transaction did not change: it is still `NpgsqlException` "Exception while reading from stream" with `TimeoutException` inside (verified), and it stays the timeout of the transaction.

## Result
- `ScriptTimeout.CreateCancellation()`: a `CancellationTokenSource` cancelled when the time left of the script runs out; `GetCancelDelay` gives no limit for `00:00:00` and for a time left longer than `MaxCancelDelay`, and cancels at once when no time is left.
- `PostgreSqlDA`: `ExecuteCommandAsync` and `QueryMultipleAsync` create their command with `CommandTimeout = 0` (`CreateScriptCommand`) and pass the token of `CreateCancellation` to every call; an `OperationCanceledException` after the token was cancelled, in a transaction that System.Transactions did not abort, becomes `ExecutionTimeoutException`. `IsCommandTimeout` is gone. The inner exception of `ExecutionTimeoutException` on PostgreSQL is now `OperationCanceledException` with `PostgresException` 57014 inside.
- Release notes corrected: 0096-59 and 0096-84 named the error of Npgsql as the inner exception of the timeout on PostgreSQL, and 0096-84 said that a timeout under one second is one second, which is now so on SQL Server only.
- No change to the `dblive` tables.
- Docs: `docs/llms-full.txt` §10, §16, §17; `docs/project/configuration.md`; the doc comment of `ExecutionTimeoutException`.
- Unit tests (`ScriptTimeoutTests`): the cancel delay (time left, under one second, no time left), no limit for `00:00:00` and 60 days, `MaxCancelDelay` is the largest delay `CancelAfter` takes, the token is cancelled when the time runs out.
- Integration tests, PostgreSQL (`CommandTimeoutTests`): a migration with `None` and a SQL test, each a loop of 5 × (`raise notice`, `pg_sleep(1)`) with a timeout of 3 s, fail with `ExecutionTimeoutException`; before the fix both ran to their end (verified). A SQL test of `select 1` and `pg_sleep(10)` with 1 s fails with `ExecutionTimeoutException`. The migration test checks the inner `OperationCanceledException` with 57014.
- All tests pass: `DbLive.Tests` 641, `DbLive.PostgreSQL.Tests` 95, `DbLive.MSSQL.Tests` 160.
