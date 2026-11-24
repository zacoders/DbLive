---
type: docs
priority: medium
created: 2026-10-31
---
# Testing that a procedure raises an error

## Problem
tSQLt has `ExpectException`. In DbLive an error in the act fails the test, and the docs show no way to test that a procedure rejects bad input. Validation errors are part of a procedure's contract, so this is a common test.

Not verified yet:
- SQL Server: `begin try exec ...; throw 50000, 'Expected an error.', 0; end try begin catch ... end catch` inside the test transaction. An error inside `try` can doom the transaction (`xact_state() = -1`), after which the test cannot run more statements. DbLive does not set `XACT_ABORT` (no match in `src` except a demo undo script); a procedure may set it itself. Does the runner report a doomed transaction clearly?
- PostgreSQL: a `do $$ ... exception when ... $$` block. The block must not catch its own "expected an error" exception. Does the act still work when it is a `call`?

## Plan
- [ ] Find working patterns on both engines and add them as demo tests (`src/Demo/DemoMSSQL`, `src/Demo/Demo.PostgreSQL.Chinook`), including a check of the error message.
- [ ] Document them.
- [ ] Decide whether an assert is worth adding, for example `select assert = 'error'` with the expected message after it. Problem: the act throws, so the assert result set would come before the act, and an error in the arrange must not pass the test. If yes, make it a separate feature task. Proposal below.

## Proposal: an `error` assert
2026-11-24, recorded at the request of the maintainer, not decided. The assert result set stands before the act, and the act raises the error as the next statement:

```sql
-- Arrange
insert into dbo.Customers (CustomerId) values (1);

-- Assert
select 'error' as assert, 'Order id must be positive.' as message;

-- Act
exec dbo.AddOrder @CustomerId = 1, @Id = -1;
```

PostgreSQL, without a `do $$ ... exception ... $$` block:
```sql
select 'error' as assert, 'order id must be positive' as message;
call add_order(1, -1);
```

Rules:
- The runner reads the result sets in order. An error before the `error` assert result set fails the test, as today, so an error in the arrange does not pass it. This answers the problem in the Plan above.
- An error after it passes the test. With a `message` column, the message of the error must match it.
- No error: the test fails, `Expected an error, but the test ran without one.`
- The expected message is a column of the assert result set, not a result set after it: the act may return result sets before it raises the error, and these are not expected ones.
- Only an error of the server counts (`SqlException`, `PostgresException`). A timeout or a broken connection fails the test.

What the code needs (from the code, not verified):
- [ ] The data access returns the result sets read before an error together with the error. Now `ExecuteQueryMultipleAsync` throws, and they are lost (`QueryMultipleAsync` in `src/DbLive.MSSQL/MsSqlDA.cs`, the same loop in `src/DbLive.PostgreSQL/PostgreSqlDA.cs`).
- [ ] Verify that SqlClient and Npgsql raise the error at the `NextResult` of the statement that failed, after the result sets before it. Most of all on SQL Server an error after which the batch goes on (547 in the arrange): is it raised before the assert result set is read?
- [ ] The transaction after the error. SQL Server: on an error `ExecuteGuardedAsync` rolls back what `IMPLICIT_TRANSACTIONS` opened after the end of the transaction, and throws the error of the script, which wins over the ended transaction (its `catch` in `MsSqlDA.cs`). So `XACT_ABORT ON` and a procedure's `begin catch if @@trancount > 0 rollback; throw; end catch` do not fail the test. After the expected error the runner must not check `IsTransactionUsableAsync` (`src/DbLive/Deployers/Testing/UnitTestItemRunner.cs`): after `XACT_ABORT` it would fail the test with `TransactionEndedMessage`. PostgreSQL: the transaction is aborted, the rest of the script and the check of `AddTransactionCheck` do not run, and the scope rolls it back.

Better than the `try`/`catch` patterns: DbLive catches the error, not the script, so the script need not survive it. The patterns break on a procedure that rolls back in its `catch` (0096-76, Notes below) and on a doomed transaction, and on PostgreSQL the act needs a `do` block.

Limits:
- The act is the last statement. On PostgreSQL nothing runs after the error. On SQL Server an error that ends only its statement (547, `raiserror`) does not stop the batch: the rest runs, is rolled back and is not checked.
- The data after the error cannot be checked in the same test ("raised the error and wrote nothing"): the transaction is aborted on PostgreSQL and may be doomed on SQL Server. Such a test keeps a `try`/`catch` pattern.
- With the mandatory assert ("Mandatory assert" in `0099-unit-testing-improvements.md`) `error` is one more assert value; a test that checks an error with it needs no other.

Open questions:
- [ ] Build it?
- [ ] How is the error compared: the exact message, a `like` pattern, the number of the error (SQL Server error number, PostgreSQL SQLSTATE)? Compare with the parameters of tSQLt `ExpectException`, from its current docs.

## Docs to update
- `docs/llms-full.txt` §8.3 (custom checks), §8.4 (example)
- `docs/unit-testing/index.md` ("Custom checks")
- Comparison page (0099-05): `ExpectException` row
- If the `error` assert is built: the assert tables of `docs/llms-full.txt` §8.3, `docs/unit-testing/index.md` ("Assertions") and `README.md`

## Done when
- [ ] Patterns verified on SQL Server and PostgreSQL with demo tests
- [ ] Docs updated
- [ ] Decision on a built-in assert recorded in Notes

## Notes
- 2026-11-03: moved from #96 (0096-24) to #99.
- 2026-11-15, from 0096-76: a test whose transaction ended before the test did fails ("The transaction of the test ended before the test did ..."). On SQL Server the usual handler of a procedure, `begin catch if @@trancount > 0 rollback; throw; end catch`, ends the transaction of the test when the test catches the error, so the patterns here must not roll back the test transaction: the procedure rolls back to a savepoint of its own inside a transaction of its caller (`docs/llms-full.txt` §8.2, "Code that rolls back the transaction" in `docs/unit-testing/index.md`). Verified on SQL Server 2025 with this test of such a procedure, a `rows` assertion on the error message and on the arrange data (`SqlTestTransactionTests` in `DbLive.MSSQL.Tests`):
  ```sql
  begin try
      exec dbo.AddOrder @Id = -1;
      throw 50001, 'Expected an error.', 1;
  end try
  begin catch
      if error_number() = 50001 throw;
      select error_message() as Error;
  end catch
  ```
  Still open here: an error that dooms the transaction, PostgreSQL, the docs of the test side.
- 2026-11-24, decision of the maintainer: the assert becomes mandatory after 2.0.0 ("Mandatory assert" in `0099-unit-testing-improvements.md`). Then a pattern that checks with `throw` ends with the new assert value, or checks the error message with `rows` as the pattern above does.
