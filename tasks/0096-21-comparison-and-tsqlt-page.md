---
type: docs
priority: medium
created: 2026-10-31
---
# Comparison page and "Coming from tSQLt"

## Goal
People evaluating DbLive compare it with "Flyway / DbUp / grate for migrations + tSQLt for tests". An outside review (2026-10-31) did exactly that and found the gaps on its own. A page that states them first, in the same honest tone as the "what DbLive guarantees / what the team owns" part of `docs/database-as-service.md`, answers most of the "worse than" points before they are raised.

## Plan
- [ ] Comparison table: DbLive vs Flyway, DbUp, grate (migrations) and tSQLt (tests). Rows: install into the database (tSQLt needs CLR and its framework objects); engines; tests as `.sql` files; rollback per test; dependency isolation (FakeTable, SpyProcedure: none in DbLive, see 0096-25, 0096-29); assertions; expected errors (0096-24); xUnit and Testcontainers; automatic undo-script tests (`[SqlDeployFact]`); downgrade; breaking phase; CLI (none, `docs/llms-full.txt` §19); maturity.
- [ ] Facts about the other tools from their current docs, not from memory; date the page.
- [ ] "Coming from tSQLt" mapping: `AssertEqualsTable` → `rows`; `AssertResultSetsHaveSameMetaData` → `rows-with-schema`; `AssertEmptyTable` → `empty`; `ExpectException` → 0096-24; `FakeTable` → 0096-25; `SpyProcedure`, `FakeFunction` → no equivalent; test classes (schemas) → folders with `setup.test.sql`; `tSQLt.Run` → `[SqlFact]` in Test Explorer / `dotnet test`.
- [ ] Where: `docs/comparison.md` (top level) or under `docs/features/`? Add to `mkdocs.yml` nav.

## Docs to update
- New page and `mkdocs.yml` nav
- `docs/llms-full.txt` §1 (when to use, link) and §19 (limitations that the comparison names)
- `README.md`, `docs/index.md` (link)

## Done when
- [ ] Page published, `mkdocs build --strict` passes
- [ ] Linked from README and docs home
