---
type: feature
priority: medium
created: 2026-11-01
---
# Deployment test: compare the schema after the undo and after a second breaking run

## Problem
The `[SqlDeployFact]` undo modes deploy each version as migration → undo → migration (`MigrationUndoMigration`) or migration → breaking → breaking → undo → migration (`MigrationBreakingUndoMigration`, `MigrationVersionDeployer`). The test fails only when an item throws.

An undo that runs without errors but does not restore the schema passes:
- the breaking script dropped the old column, the undo does not add it back; the repeated migration does not touch that column and succeeds. This is the case that matters for a rollback: the old application needs the column.
- the undo leaves an index, a constraint or a table created by the migration, and the migration is tolerant (`if not exists`).

A breaking script whose second run changes more than the first one passes too, and then the undo is tested against a state that production never has. In production a migration breaking script runs once, and a rollback runs the undo after that one run; the test runs the undo after two runs. Example:

```sql
-- the condition is checked before the trigger is dropped
if object_id('dbo.TMP_sync') is null
    alter table dbo.Person drop column if exists Phone;
drop trigger if exists dbo.TMP_sync;
```

The first run drops only the trigger, the second one also drops the column. The test checks the undo against "both dropped"; an undo that does not handle "trigger dropped, column still there" passes the test and fails the real rollback. The SQL tests do not catch it: they see the state after a single breaking run (the breaking phase at the end of the deployment runs the reverted breaking script once).

The same holds for the breaking scripts of the Code folder (0096-15): the second deployment of `DeployFixture` (`DeployParameters.Breaking`) runs them again and fails only when one throws.

## Where
- `src/DbLive/Deployers/Migrations/MigrationVersionDeployer.cs:24-42` (undo modes)
- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs`
- `src/DbLive.xunit/Deploy/SqlDeployFactDiscoverer.cs`, `src/DbLive.xunit/Deploy/DeployFixture.cs` (the second breaking deployment)

## Fix
A schema snapshot, compared at three points:

1. **Undo** (both undo modes, a version with an undo script): the snapshot before the migration equals the one after the undo.
2. **Second breaking run** (`MigrationBreakingUndoMigration`, a version with a breaking script): the snapshot after the first breaking run equals the one after the second. A difference fails with "the breaking script is not idempotent: the second run changed ...". With 1 and 2 together the undo after two runs is also tested against the state after one run, so the sequence migration → breaking → breaking → undo → migration stays and the double run stays: it is what this check compares.
3. **Second breaking deployment** of `DeployFixture` (the cases with breaking): the snapshot before it equals the one after it. It runs the breaking scripts of the Code folder and BeforeDeploy/AfterDeploy a second time; none of them may change the schema on a second run.

- [ ] Snapshot through the catalog of each engine (`sys.*` / `pg_catalog`): a new `IDbLiveDA` member returning a provider-neutral model, compared in `DbLive`.
- [ ] What is compared: schemas; tables; columns (type, length/precision, nullability, default, identity, computed, collation); primary key, unique, foreign key and check constraints; indexes (columns, order, included columns, filter, unique); views, functions, procedures, triggers (definitions); PostgreSQL types/enums and sequences (definition, not the current value).
- [ ] What is ignored: DbLive's own schema (`dblive`); object ids; current identity and sequence values; statistics.
- [ ] System-generated names. SQL Server names unnamed constraints itself (`DF__users__name__3A81B327`), so an undo that recreates a column with its default gets a different name. Compare such constraints by definition and ignore the name when it is system-generated (`is_system_named`)?
- [ ] Column order. An undo that adds a dropped column back puts it at the end. Compare by name only, or also by position (matters for `select *` and `insert` without a column list)? PostgreSQL keeps dropped columns in `pg_attribute` (`attisdropped`): use names, not `attnum`.
- [ ] Failure: a new exception (or one per check) that names the check (undo, second breaking run, second breaking deployment), the script, and lists missing, extra and changed objects, readable in the xUnit output.
- [ ] Only in `[SqlDeployFact]` (undo modes and the second breaking deployment); production deployments are not affected.
- [ ] Data is not compared: a breaking script whose second run deletes more rows still passes. See `tasks/ideas/undo-test-data-round-trip.md`.

## Docs to update
- `docs/llms-full.txt` §5.3, §5.5, §6.1, §13.3, §17
- `docs/unit-testing/index.md` ("Deployment tests"), `docs/deployment/migrations.md` ("Undo scripts"), `docs/deployment/breaking-changes.md` ("In tests"): "a script that does not survive a second run" becomes "a script whose second run fails or changes the schema"

## Done when
- [ ] Implemented on SQL Server and PostgreSQL, with unit tests for the comparison
- [ ] Integration tests on both engines: an undo that does not restore a column, an index and a constraint fails with a readable list of differences; a migration breaking script and a code breaking script whose second run drops more fail; correct scripts pass
- [ ] Docs updated

## Notes
Found while discussing 0096-15. A data check could reuse the same snapshot: `tasks/ideas/undo-test-data-round-trip.md`; not part of this task.

2026-11-01: the second breaking run (point 2) and the second breaking deployment (point 3) were added after 0096-15 introduced them; until this task is done they fail only when a script throws.
