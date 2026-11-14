---
type: feature
priority: low
created: 2026-10-31
---
# Test helpers FakeTable, SpyProcedure and FakeFunction

## Goal
Let a test isolate the object under test from its dependencies, as tSQLt does: replace a table with an empty copy without constraints, replace a procedure with one that records its calls, replace a function with a fake. Without this a procedure that writes to a table with many foreign keys, or calls `dbo.SendEmail`, gets only a long integration test: the Arrange fills every referenced table, and the test really sends the e-mail. An outside review (2026-10-31) named this the main weakness of DbLive compared with tSQLt. It also solves the foreign keys problem: `FakeTable` removes every constraint of the table, so a test needs no recipe that switches foreign keys off.

## Decision
2026-11-03, maintainer: helper procedures in the `dblive` schema with the names, parameters and behavior of tSQLt. Being different from tSQLt ("no framework installed into the database") is not a goal; convenience for the user is. Reasons:
- Tests are now written mostly by LLMs, and LLMs know the tSQLt API well. `tSQLt.FakeTable` becomes `dblive.FakeTable`: nothing new to learn, the first attempt is right.
- A reviewer reads one line instead of a page of DDL.
- The engine pitfalls (one batch per test file on SQL Server, `SCHEMABINDING`, PostgreSQL overloads and OID binding) are solved and tested once, inside the helper, not again in every test.

Rejected:
- A documented recipe only (rename and create by hand): every test repeats the DDL, and on SQL Server `create procedure` / `create function` needs `exec('...')`, because a test file is one batch.
- Helpers as session-temporary objects (`#dblive_fake_table` on SQL Server, `pg_temp.` functions on PostgreSQL): unknown to LLMs, and PostgreSQL needs the `pg_temp.` prefix in every call.
- Directives in the test file (`-- dblive: fake-table dbo.Orders`) with the SQL generated in C#: a new mini-language, and the most code.
- Helpers installed by the xUnit fixture only: `RunAllTestsAsync` on a database that is already deployed, and NUnit or MSTest with the fixture builders, would not have them.

## Design
SQL Server:
```sql
-- Arrange
exec dblive.FakeTable 'dbo.Orders';
exec dblive.SpyProcedure 'dbo.SendEmail';

-- Act
exec dbo.PlaceOrder @CustomerId = 1;

-- Assert: one e-mail was sent
select [To] from dbo.SendEmail_SpyProcedureLog;
select 'rows' as assert;
select 'customer1@example.com' as [To];
```

PostgreSQL:
```sql
call dblive.FakeTable('public.orders');
call dblive.SpyProcedure('public.send_email');

call public.place_order(1);

select recipient from public.send_email_SpyProcedureLog;
select 'rows' as assert;
select 'customer1@example.com' as recipient;
```

- **Names and parameters as in tSQLt**; check them against the current tSQLt docs, not from memory. `FakeTable` (`@TableName`, `@SchemaName`, `@Identity`, `@ComputedColumns`, `@Defaults`): renames the table and creates an empty copy without constraints. `SpyProcedure` (`@ProcedureName`, `@CommandToExecute`): replaces the procedure with one that writes every call to `<procedure>_SpyProcedureLog`, in the procedure's schema, with `_id_` and one column per parameter. `FakeFunction` (`@FunctionName`, `@FakeFunctionName`, `@FakeDataSource`): replaces the function with another function, or a table-valued function with a data source.
- **Undone by the rollback.** Each test runs in a transaction that is always rolled back (`src/DbLive/Deployers/Testing/UnitTestItemRunner.cs`), and DDL is transactional on both engines, so the rollback restores the original objects. The helpers create the replacements with dynamic SQL, so the one-batch limit of SQL Server test files does not matter.
- **PostgreSQL: procedures, called with `call`.** `select dblive.FakeTable(...)` would return a result set, and the test would count it as an actual result set. The objects are created with unquoted names, so `dblive.FakeTable` and `dblive.faketable` are the same.
- **Installed with the `dblive` schema** by every `DeployAsync`, as internal migrations: `src/DbLive.MSSQL/SQL/11.test-helpers.sql`, `src/DbLive.PostgreSQL/SQL/08.test-helpers.sql` (`src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs`). Every deployed database has them, production too; a later change to a helper is a new internal migration.
- **Work only inside a DbLive test.** The test runner marks each test connection inside the test transaction: SQL Server `sp_set_session_context`, PostgreSQL `set_config('dblive.test', 'on', true)` (local to the transaction). Without the mark a helper throws `dblive.FakeTable can only be called from a DbLive test.` A call by mistake in a migration, or by hand in production, changes nothing.
- **Run alone.** A test that calls a helper holds a schema lock on the replaced object until the rollback (SQL Server `Sch-M`, PostgreSQL `ACCESS EXCLUSIVE`), so other tests that touch the object wait, and with `Serializable` can deadlock. `RunAllTestsAsync` runs 10 tests at a time (`src/DbLive/Deployers/Testing/UnitTestsRunner.cs`); it finds the helper names in the test and its `setup.test.sql` and runs these tests one at a time, after the others.
- **Limits, with a clear error instead of a test that silently checks the wrong thing:**
  - SQL Server: a table or function referenced by a `WITH SCHEMABINDING` object cannot be renamed.
  - PostgreSQL: views and functions with a `BEGIN ATOMIC` body are bound to the table by OID; after the rename they still read the original table, not the fake.
  - Foreign keys of other tables still reference the original table (as in tSQLt).
  - A database deployed by an older DbLive has no helpers: the error says to deploy it with the current version.

## Plan
- [ ] Verify the mechanism on both engines in integration tests: rename and create inside the test transaction, the rollback restores the original; DDL in `setup.test.sql` (a separate connection in the same transaction) is visible to the test and does not block it; schema locks with tests running in parallel.
- [ ] SQL Server helpers: internal migration `11.test-helpers.sql`.
- [ ] PostgreSQL helpers: internal migration `08.test-helpers.sql`.
- [ ] The mark on test connections, and the check in every helper.
- [ ] `RunAllTestsAsync`: tests that call a helper run one at a time, after the others.
- [ ] Integration tests for each helper on each engine: the replacement works, the rollback restores, the call outside a test fails, the limits above give their errors.
- [ ] A demo test for each helper.

Open questions:
- [ ] PostgreSQL: `SpyProcedure` also for functions that return `void` (PostgreSQL code often uses them where SQL Server uses procedures)? Overloads: the name with argument types, `'public.send_email(text, text)'`?
- [ ] SQL Server `FakeFunction`: the fake function must exist before the call, and `create function` needs a batch of its own (`exec('...')` in the test, or `setup.test.sql`, which allows `GO`). Add a parameter for a scalar fake that only returns a given value?
- [ ] SQL Server: is the session context cleared when a pooled connection is reused (`sp_reset_connection`)? If not, the mark must be removed after the test.
- [ ] Do `[SqlFact]` cases run one at a time? One test method gives one xUnit test class, so they should; not verified.
- [ ] More tSQLt helpers later (`ApplyConstraint`, `ApplyTrigger`, `RemoveObject`)? Separate tasks if needed.

## Docs to update
- `docs/unit-testing/index.md` (new section: isolating dependencies)
- `docs/llms-full.txt` §8 (helpers and examples), §15 (helpers in the `dblive` schema), §16 (`exec` vs `call`), §17 (new errors)
- `docs/deployment/index.md`, where it lists the `dblive` schema
- Comparison page (0099-05): `FakeTable`, `SpyProcedure`, `FakeFunction` rows, "install into the database" row

## Done when
- [x] Design agreed with the maintainer (2026-11-03)
- [ ] Helpers on SQL Server and PostgreSQL, with integration tests
- [ ] Helpers work only inside a test; tests that call them run one at a time
- [ ] Docs updated

## Notes
- 2026-11-03: moved from #96 (0096-29) to #99.
- 2026-11-03, analysis (from the code and the docs, not verified): the need is real, but the mechanism already exists. A test can rename a table or procedure and create a replacement by hand today, and the rollback restores it; the helpers make it one line and hide the pitfalls listed under Design.
