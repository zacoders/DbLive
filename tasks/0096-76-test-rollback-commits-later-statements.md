---
type: bug
priority: medium
created: 2026-11-12
---
# A SQL test whose code rolls back the transaction passes and leaves data behind (SQL Server)

## Problem
The docs promise that a test leaves nothing behind (`docs/llms-full.txt` §8.2, step 5: "Always roll back. Tests leave no data behind and can be re-run").

On SQL Server the code under test can roll back the test transaction itself. The usual error handler of a procedure does it:

```sql
begin catch
	if @@trancount > 0 rollback;
	throw;
end catch
```

`rollback` ends the transaction of the test, not only the one the procedure opened. The rest of the test file then runs in autocommit mode: every statement is committed at once. `Transaction.Current` stays `Active`, so the runner does not notice, and the rollback at the end of the test has nothing to undo.

Verified on SQL Server 2022 (a temporary harness, not kept). `dbo.Fail` opens a transaction, fails and has the handler above. The test:

```sql
insert into dbo.T (Id, Note) values (100, 'arrange');
begin try
	exec dbo.Fail;
end try
begin catch
	insert into dbo.T (Id, Note) values (200, error_message());
end catch
select count(*) as cnt from dbo.T where Id = 200;
select 'rows' as assert;
select 1 as cnt;
```

`RunAllTestsAsync` reports the test as passed (`dblive.unit_test_run.pass = 1`), and `dbo.T` keeps the row `200, boom` after the run. Every run adds one more row.

So:
- the test passes although its arrange data is gone: the row `100` was rolled back in the middle of the test, and the assertions after the call run against other data than the test prepared;
- what the test writes after the call stays in the database. In a test database of `[SqlFact]` it shows up in other tests. With `RunAllTestsAsync` against a database that is already deployed it stays for good.

Tests of the error path of a procedure are common (0099-03 is about them), and so is this handler.

PostgreSQL should not be affected (from its semantics, not verified): a procedure cannot end the transaction of its caller there (`2D000: invalid transaction termination`), and an error aborts the transaction.

Found by the code review of 2026-11-12 (finding 3).

## Where
- `src/DbLive/Deployers/Testing/UnitTestItemRunner.cs:26-60` (the transaction of the test; nothing checks it after the script)
- `src/DbLive.MSSQL/MsSqlDA.cs:13-19` (`IsTransactionUsableAsync`, named `IsTransactionUsableAfterErrorAsync` before 0096-68: `@@trancount` on a connection of the scope)

## Fix
After the test script (and after `setup.test.sql`), still inside the scope, ask the provider whether the transaction is open, with the check that 0096-61 added for code files. When it is not, the test fails with a message that says what happened: the test or the code it calls rolled back the test transaction, the statements after the rollback were committed, and how to test such a procedure (0099-03).

Verified the same way: after the test above `IsTransactionUsableAfterErrorAsync()` returns `false`, after a test that does not roll back it returns `true`.

The statements that were already committed cannot be undone; the failed test tells the author to change the test.

- [x] Rename `IsTransactionUsableAfterErrorAsync`? It would also be called after a test that ran without an error. Decided by the maintainer on 2026-11-14: yes, renamed to `IsTransactionUsableAsync` in 0096-68. It has no side effects. On PostgreSQL it is a query now, `false` in an aborted transaction (`25P02`), so a test on PostgreSQL costs one more query too.
- [ ] One more query per test on SQL Server. Acceptable?

## Docs to update
- `docs/llms-full.txt` §8.2 (execution: what happens when the transaction is rolled back), §16 (provider differences), §17
- `docs/unit-testing/index.md`
- `tasks/0099-03-expected-error-tests.md`: the patterns it documents must not roll back the test transaction

## Done when
- [ ] A test whose transaction was rolled back by its code fails with a clear message; integration test on SQL Server
- [ ] Docs updated
