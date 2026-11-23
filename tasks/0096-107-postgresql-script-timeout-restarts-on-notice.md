---
type: bug
priority: medium
created: 2026-11-23
---
# PostgreSQL: a script that sends notices runs past its timeout

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
- `src/DbLive.PostgreSQL/PostgreSqlDA.cs:246-261` (`ExecuteCommandAsync`) and the other places that set `CommandTimeout` from `ScriptTimeout` (a code file in its own transaction, the reader of the tests).

## Fix
Enforce the deadline in DbLive: a `CancellationTokenSource` with `CancelAfter(timeout)`, passed to `ExecuteNonQueryAsync` / `ExecuteReaderAsync`. Map the cancellation (Npgsql throws `OperationCanceledException` with a `57014` `PostgresException` inside) to `ExecutionTimeoutException`, as the command timeout is mapped now. Keep the exclusion for a transaction that System.Transactions aborted (`IsTransactionAborted`).

- [ ] Keep `CommandTimeout` as well, as a second limit?

## Docs to update
None if fixed. If it is not fixed: `docs/llms-full.txt` §10 and §16.

## Done when
- [ ] Fixed, with a PostgreSQL integration test: a script that sends a notice every 2 s fails at its timeout of 3 s with `ExecutionTimeoutException`
