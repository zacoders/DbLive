---
type: docs
priority: medium
created: 2026-10-31
---
# Testing a procedure without filling every referenced table

## Problem
DbLive has no `FakeTable`. A procedure that writes to a table with many foreign keys needs an Arrange that fills every referenced table, so tests become long integration tests. An outside review (2026-10-31) named this the main weakness compared with tSQLt.

Each test runs in a transaction that is always rolled back, so schema changes made inside a test are rolled back too. That may give a workaround without new features. Not verified yet:
- SQL Server: `alter table dbo.Orders nocheck constraint all` in the arrange. DDL is transactional, so the rollback restores the constraints. It holds a schema lock until the rollback: other tests that touch the table wait. Deployment runs tests in 10 threads; do `[SqlFact]` cases run one at a time?
- PostgreSQL: disabling FK triggers (`alter table ... disable trigger all`) needs superuser. Alternative: `alter table ... alter constraint <fk> deferrable initially deferred` plus `set constraints all deferred`: deferred checks run at commit, and a test never commits.
- `setup.test.sql` runs on a separate connection in the same transaction (`docs/unit-testing/index.md`). Does DDL there block the test's connection?

## Plan
- [ ] Try both approaches in demo tests, with tests running in parallel.
- [ ] If they work, document the recipe with its limits (locks, permissions); otherwise record why not.
- [ ] Results feed 0096-29 (helpers).

## Docs to update
- `docs/unit-testing/index.md` (new section)
- `docs/llms-full.txt` §8 (recipe), §19 if it does not work on an engine
- Comparison page (0096-21): `FakeTable` row

## Done when
- [ ] Verified on SQL Server and PostgreSQL with demo tests
- [ ] Recipe documented, or the limitation documented
