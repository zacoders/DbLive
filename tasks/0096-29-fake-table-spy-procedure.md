---
type: feature
priority: low
created: 2026-10-31
---
# Test helpers in the spirit of FakeTable and SpyProcedure

## Goal
Let a test isolate the object under test from its dependencies: replace a table with an empty copy without constraints, replace a called procedure with one that records its calls. These are the tSQLt features DbLive users would miss most.

Depends on 0096-25: if constraints can be switched off inside a test, `FakeTable` may not be needed.

## Plan
Open questions:
- [ ] Form: procedures in the `dblive` schema (`dblive.fake_table 'dbo.Orders'`, `dblive.spy_procedure 'dbo.SendEmail'`), working inside the test transaction (rename the object, create a replacement; the rollback restores it).
- [ ] Opt-in only, and never in a production database: deployed by the xUnit fixture, not by `DeployAsync`? "No framework installed into the database" is an advantage over tSQLt that should stay.
- [ ] Both engines? PostgreSQL may differ (permissions, renaming tables with dependent views).
- [ ] Interaction with parallel tests (schema locks).

## Docs to update
- `docs/unit-testing/index.md`, `docs/llms-full.txt` §8
- Comparison page (0096-21)

## Done when
- [ ] Design agreed with the maintainer
- [ ] Implemented with tests on the supported engines, or the decision not to recorded in Notes
