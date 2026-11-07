---
type: docs
priority: medium
created: 2026-10-31
---
# Comparison page and "Coming from tSQLt"

## Goal
People evaluating DbLive compare it with "Flyway / DbUp / grate for migrations + tSQLt for tests". An outside review (2026-10-31) did exactly that and found the gaps on its own. A page that states them first, in the same honest tone as the "what DbLive guarantees / what the team owns" part of `docs/database-as-service.md`, answers most of the "worse than" points before they are raised.

## Plan
- [ ] Comparison table: DbLive vs Flyway, DbUp, grate (migrations) and tSQLt (tests). Rows: install into the database (tSQLt needs CLR and its framework objects; DbLive deploys its `dblive` schema, with the test helpers of 0099-01, and needs no CLR); engines; tests as `.sql` files; rollback per test; dependency isolation (`dblive.FakeTable`, `dblive.SpyProcedure`, `dblive.FakeFunction`: 0099-01); assertions; expected errors (0099-03); xUnit and Testcontainers; deployment test (`[SqlDeployFact]`: the release and the breaking phase with schema checks); rollback (no undo scripts by design: the breaking phase keeps the previous release working, 0096-46); breaking phase; CLI (none, `docs/llms-full.txt` §19); maturity.
- [ ] Facts about the other tools from their current docs, not from memory; date the page.
- [ ] "Coming from tSQLt" mapping: `AssertEqualsTable` → `rows`; `AssertResultSetsHaveSameMetaData` → `rows-with-schema`; `AssertEmptyTable` → `empty`; `ExpectException` → 0099-03; `FakeTable`, `SpyProcedure`, `FakeFunction` → `dblive.FakeTable`, `dblive.SpyProcedure`, `dblive.FakeFunction` with the same parameters (0099-01); test classes (schemas) → folders with `setup.test.sql`; `tSQLt.Run` → `[SqlFact]` in Test Explorer / `dotnet test`.
- [ ] Where: `docs/comparison.md` (top level) or under `docs/features/`? Add to `mkdocs.yml` nav.

## Docs to update
- New page and `mkdocs.yml` nav
- `docs/llms-full.txt` §1 (when to use, link) and §19 (limitations that the comparison names)
- `README.md`, `docs/index.md` (link)

## Done when
- [ ] Page published, `mkdocs build --strict` passes
- [ ] Linked from README and docs home
