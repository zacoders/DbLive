---
type: feature
priority: medium
created: 2026-11-01
closed: 2026-11-01
breaking: true
---
# Deployment test: compare the schema after the undo and after a second breaking run

## Release note
**The deployment test compares the schema.** `[SqlDeployFact]` checks that the undo of a migration restores the schema the database had before the migration, and that a second run of the whole breaking phase changes nothing. A difference fails the test with `SchemaChangedException`, which lists the missing, extra and changed objects (`missing column dbo.Person.PhoneNumber`). Before, an undo that ran without errors but did not restore the schema passed. Fix the undo and breaking scripts that start failing. The schema is read from the catalog: SQL Server 2017 or later, PostgreSQL 12 or later. Production deployments do not change.

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

- [x] Snapshot through the catalog of each engine (`sys.*` / `pg_catalog`): a new `IDbLiveDA` member returning a provider-neutral model, compared in `DbLive`.
- [x] What is compared: schemas; tables; columns (type, length/precision, nullability, default, identity, computed, collation); primary key, unique, foreign key and check constraints; indexes (columns, order, included columns, filter, unique); views, functions, procedures, triggers (definitions); PostgreSQL types/enums and sequences (definition, not the current value).
- [x] What is ignored: DbLive's own schema (`dblive`); object ids; current identity and sequence values; statistics.
- [x] System-generated names. SQL Server names unnamed constraints itself (`DF__users__name__3A81B327`), so an undo that recreates a column with its default gets a different name. Compare such constraints by definition and ignore the name when it is system-generated (`is_system_named`)? Yes (decided 2026-11-01).
- [x] Column order. An undo that adds a dropped column back puts it at the end. Compare by name only, or also by position (matters for `select *` and `insert` without a column list)? PostgreSQL keeps dropped columns in `pg_attribute` (`attisdropped`): use names, not `attnum`. By name only (decided 2026-11-01).
- [x] Failure: a new exception (or one per check) that names the check (undo, second breaking run, second breaking deployment), the script, and lists missing, extra and changed objects, readable in the xUnit output.
- [x] Only in `[SqlDeployFact]` (undo modes and the second breaking deployment); production deployments are not affected.
- [x] Data is not compared: a breaking script whose second run deletes more rows still passes. See `tasks/ideas/undo-test-data-round-trip.md`.

## Docs to update
- `docs/llms-full.txt` §5.3, §5.5, §6.1, §13.3, §17
- `docs/unit-testing/index.md` ("Deployment tests"), `docs/deployment/migrations.md` ("Undo scripts"), `docs/deployment/breaking-changes.md` ("In tests"): "a script that does not survive a second run" becomes "a script whose second run fails or changes the schema"

## Done when
- [x] Implemented on SQL Server and PostgreSQL, with unit tests for the comparison
- [x] Integration tests on both engines: an undo that does not restore a column, an index and a constraint fails with a readable list of differences; a migration breaking script and a code breaking script whose second run drops more fail; correct scripts pass
- [x] Docs updated

## Notes
Found while discussing 0096-15. A data check could reuse the same snapshot: `tasks/ideas/undo-test-data-round-trip.md`; not part of this task.

2026-11-01: the second breaking run (point 2) and the second breaking deployment (point 3) were added after 0096-15 introduced them; until this task is done they fail only when a script throws.

2026-11-01, implementation:
- Model (`DbLive.Schema`): a snapshot is a flat set of `SchemaObject(Type, Name, Definition, Parent)`; the name identifies the object within its type, the definition is text in the engine's own form. Comparing is a dictionary diff (`SchemaDiff`: missing, extra, changed). `Parent` only folds the content of a missing or extra table/schema into one line of the message.
- One query per engine, `MsSqlSchemaSnapshot` / `PostgreSqlSchemaSnapshot`, one result set `(object_type, object_name, parent_name, definition)`, strings built in SQL.
- SQL Server 2017+ (`string_agg`, `concat_ws`). The first plan was to build the lists in C# to keep 2016; dropped: every SQL Server Docker image is 2017 or later, 2016 is out of extended support since July 2026, and the check runs only in deployment tests. `*_desc` columns need `collate database_default` (they have another collation than the catalog names: "Cannot resolve collation conflict for string_agg").
- SQL Server keys of server-named constraints: default → part of its column's definition, primary key → its table, unique → table + key columns, foreign key → table + columns + referenced table, check → table + expression. Two objects with one key (two equal server-named checks) are numbered `#2`; the queries are ordered so the numbering is stable.
- Module text: SQL Server stores `create or alter` as `create   ` and `alter` as `CREATE` (verified on 2025), and keeps leading comments; the first `create` is lower-cased and whitespace collapsed. PostgreSQL views come from `pg_get_viewdef` (normalized), function bodies as written: whitespace collapsed.
- PostgreSQL: names compared as they are (generated names such as `person_pkey`, `person_age_check` depend only on table and columns); identity sequences belong to their column; objects of extensions are skipped, the extension itself is compared.
- Verified on both engines: dropping and recreating a column (it comes back last), its server-named default, check and foreign key gives an equal snapshot.

## Result
- `IDbLiveDA.GetSchemaSnapshotAsync()` reads the schema from the catalog: `MsSqlSchemaSnapshot` (SQL Server 2017+), `PostgreSqlSchemaSnapshot` (PostgreSQL 12+). Model and comparison in `DbLive.Schema`: `SchemaSnapshot`, `SchemaObject`, `SchemaDiff` (missing, extra, changed; the content of a missing or extra table is counted on its line), `SchemaChangedException` with `SchemaCheck` `Undo` / `BreakingSecondRun` / `BreakingSecondDeployment`.
- `MigrationVersionDeployer` compares the schema before the migration with the schema after the undo (both undo modes) and the schema after the first and the second breaking run; `DeployFixture` compares the schema before and after the second breaking deployment. Nothing changes for production deployments.
- Decisions: columns by name, not position; SQL Server constraints named by the server identified by table and definition; module text compared with whitespace collapsed and `create or alter`/`alter` as `create`; PostgreSQL names compared as they are.
- Tests: `SchemaDiffTests`, `MigrationVersionDeployerTests`, `DeployFixtureTests` (unit); `SchemaSnapshotTests` and `SchemaCheckTests` on both engines, including the deployment transaction (`TransactionWrapLevel: Deployment`). The demo projects pass; a broken `DemoMSSQL` undo (`005.undo.sql` that does not drop the index) fails the four undo cases with `extra index dbo.Users.IDX_Users_Name`.
- Found: `0096-32-01-deploy-fact-display-names.md` (all six `[SqlDeployFact]` cases have one display name); `0096-32-02-deploy-fact-two-cases.md` (with the schema checks, two cases in the production order replace the six).
