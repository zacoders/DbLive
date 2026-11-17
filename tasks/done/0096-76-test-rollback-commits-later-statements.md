---
type: bug
priority: medium
created: 2026-11-12
closed: 2026-11-15
breaking: true
---
# A SQL test whose code rolls back the transaction passes and leaves data behind (SQL Server)

## Release note
**A SQL test fails when its transaction ended before the test did.** On SQL Server `ROLLBACK` without a savepoint name ends the transaction of the test, also inside a procedure the test calls: the usual error handler `begin catch if @@trancount > 0 rollback; throw; end catch` does it when the test catches the error, and so does `COMMIT` or `ROLLBACK` in a test or in `setup.test.sql`. The rest of the test then ran in autocommit mode: its changes stayed in the database, and the test could pass on other data than it prepared. Now the test fails with "The transaction of the test ended before the test did: ...". What was committed still stays in the database.

To upgrade, when a test fails with this message: make the procedure roll back to a savepoint of its own when it runs inside a transaction of its caller (`save transaction`, see "Code that rolls back the transaction" in the unit testing docs), and remove `commit` and `rollback` from tests. PostgreSQL is not affected: a procedure cannot end the transaction of its caller there.

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
- [x] One more query per test on SQL Server. Acceptable? Decided by the maintainer on 2026-11-15: yes, the check after the test was chosen over the alternatives (see Notes).

## Docs to update
- `docs/llms-full.txt` §0 (rule 19), §8.2 (execution: what happens when the transaction is rolled back), §16 (provider differences), §17
- `docs/unit-testing/index.md` ("Code that rolls back the transaction")
- `tasks/0099-03-expected-error-tests.md`: the patterns it documents must not roll back the test transaction

## Done when
- [x] A test whose transaction was rolled back by its code fails with a clear message; integration test on SQL Server
- [x] Docs updated

## Notes
- 2026-11-15: alternatives considered with the maintainer. Comparing `current_transaction_id()` before and after the test would also catch a test that ends the transaction and opens a new one (`rollback; begin tran`), for one more query per test: not worth it. A check appended to the test script (`if @@trancount = 0 throw ...`) saves the query, but changes the script of the user, and a script that ends with a `--` comment or an open `/*` swallows it. Nothing prevents the rollback itself: a savepoint or a nested `begin tran` of DbLive does not help, `rollback` without a name rolls back everything.
- One check, after the test only, not also after `setup.test.sql`: whatever runs after the end of the transaction runs in autocommit mode, the test script included, so the check after the test sees a transaction that the setup file ended too (verified: the setup case of the integration test). The message names both.
- Not checked after a test whose script fails with an error: it fails anyway, with its own error.
- An exception of the check (a broken connection, a transaction that timed out) fails the test with that exception.
- On PostgreSQL the check is `select 1` and never sees this case: a procedure cannot end the transaction of its caller, `rollback` in it fails with `2D000` (verified by the integration test). It runs there too: one query, and no provider check in the runner.
- The savepoint pattern of the docs is verified on SQL Server 2025 (the image of the integration tests): the error path of such a procedure passes, the arrange data is still there after the call, and nothing stays after the test.
- Breaking: a test that passed now fails (`breaking: true`).

## Result
- `UnitTestItemRunner` calls `IDbLiveDA.IsTransactionUsableAsync()` after the test script, inside the scope. When the transaction is gone, the test fails with `UnitTestItemRunner.TransactionEndedMessage` and its assertion is not evaluated. The doc comment of `IsTransactionUsableAsync` names the second caller.
- Unit tests (`UnitTestItemRunnerTests`): the check runs after the setup file and the test, a gone transaction fails the test without evaluating the assertion, an exception of the check fails the test, a test that fails with an error is not checked.
- Integration tests: `SqlTestTransactionTests` in `DbLive.MSSQL.Tests` (a procedure with the usual handler, a setup file that ends the transaction, `commit` in a test: each fails with the message, and what ran after the end of the transaction stays; the error path of a procedure that rolls back to its savepoint passes and leaves nothing) and in `DbLive.PostgreSQL.Tests` (a procedure with `rollback` fails the test with `2D000`, nothing stays). Without the check the three SQL Server cases pass (verified).
- Docs: `docs/llms-full.txt` §0 rule 19, §8.2, §16, §17; `docs/unit-testing/index.md` ("Code that rolls back the transaction"); a note in `tasks/0099-03-expected-error-tests.md`.
- All tests of the solution pass, except `DeploymentLoginPermissionsTests`, which fails now and then on its cleanup, not related (0096-82).
