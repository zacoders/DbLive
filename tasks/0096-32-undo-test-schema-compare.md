---
type: feature
priority: medium
created: 2026-11-01
---
# Undo test: compare the schema before the migration and after the undo

## Problem
The `[SqlDeployFact]` undo modes deploy each version as migration → (breaking) → undo → migration (`MigrationVersionDeployer`). The test fails only when an item throws. An undo that runs without errors but does not restore the schema passes:
- the breaking script dropped the old column, the undo does not add it back; the repeated migration does not touch that column and succeeds. This is the case that matters for a rollback: the old application needs the column.
- the undo leaves an index, a constraint or a table created by the migration, and the migration is tolerant (`if not exists`).

## Where
- `src/DbLive/Deployers/Migrations/MigrationVersionDeployer.cs:24-40` (undo modes)
- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`
- `src/DbLive.xunit/Deploy/SqlDeployFactDiscoverer.cs`, `src/DbLive.xunit/Deploy/DeployFixture.cs`

## Fix
In both undo modes, for a version that has an undo script: take a schema snapshot before the migration, take another after the undo, and fail with the differences when they do not match.

- [ ] Snapshot through the catalog of each engine (`sys.*` / `pg_catalog`): a new `IDbLiveDA` member returning a provider-neutral model, compared in `DbLive`.
- [ ] What is compared: schemas; tables; columns (type, length/precision, nullability, default, identity, computed, collation); primary key, unique, foreign key and check constraints; indexes (columns, order, included columns, filter, unique); views, functions, procedures, triggers (definitions); PostgreSQL types/enums and sequences (definition, not the current value).
- [ ] What is ignored: DbLive's own schema (`dblive`); object ids; current identity and sequence values; statistics.
- [ ] System-generated names. SQL Server names unnamed constraints itself (`DF__users__name__3A81B327`), so an undo that recreates a column with its default gets a different name. Compare such constraints by definition and ignore the name when it is system-generated (`is_system_named`)?
- [ ] Column order. An undo that adds a dropped column back puts it at the end. Compare by name only, or also by position (matters for `select *` and `insert` without a column list)? PostgreSQL keeps dropped columns in `pg_attribute` (`attisdropped`): use names, not `attnum`.
- [ ] Failure: a new exception that lists missing, extra and changed objects, readable in the xUnit output.
- [ ] Only in the `[SqlDeployFact]` undo modes; production deployments are not affected.

## Docs to update
- `docs/llms-full.txt` §5.5, §13.3, §17
- `docs/unit-testing/index.md` ("Deployment tests"), `docs/deployment/migrations.md` ("Undo scripts")

## Done when
- [ ] Implemented on SQL Server and PostgreSQL, with unit tests for the comparison
- [ ] Integration tests on both engines: an undo that does not restore a column, an index and a constraint fails with a readable list of differences; a correct undo passes
- [ ] Docs updated

## Notes
Found while discussing 0096-15. A data check could reuse the same snapshot: `tasks/ideas/undo-test-data-round-trip.md`; not part of this task.
