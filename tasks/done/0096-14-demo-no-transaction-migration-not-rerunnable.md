---
type: chore
priority: low
created: 2026-10-31
closed: 2026-11-01
---
# DemoMSSQL 004 runs without a transaction but is not re-runnable

## Problem
`src/Demo/DemoMSSQL/Scripts/Migrations/004.migration.sql` runs with `"TransactionWrapLevel": "none"` (`004.migration.settings.json`) and creates two indexes with plain `create index`. The docs (0096-13) say a script without a transaction must be re-runnable, because a failed script runs again from the start on the next deployment. If the second index fails, the rerun fails on the first one ("already exists"). The demo is the example users copy.

## Where
`src/Demo/DemoMSSQL/Scripts/Migrations/004.migration.sql`, `004.undo.sql` (the undo comment describes the same risk).

## Fix
- [x] Wrap each `create index` in `if not exists (select * from sys.indexes where name = ... and object_id = object_id(...))`.
- [x] Editing an applied migration changes its checksum: databases that already have 004 fail with `MigrationChecksumMismatchException`. The schema after the edit is the same, so such a database is deployed once with `RepairMigrationChecksums = true` (or recreated).
- [x] Consider an undo settings file for 004 if the undo drops the indexes online. Not needed, see Notes.

## Done when
- [x] Demo 004 is re-runnable, demo tests green

## Notes
2026-11-01:
- Verified: SMO `ServerConnection.ExecuteNonQuery` (`MsSqlDA.ExecuteNonQueryAsync`) runs the `go` batches one by one; without a transaction each commits on its own. `MigrationItemDeployer` marks the item `applied` only after the whole script, so a failure after the first index leaves it in place and the next deployment runs 004 again. On a SQL Server 2025 container with only `IDX_Orders_OrderDate` created, the old 004 fails with `Msg 1913 ... already exists`; the new 004 creates the second index and runs again without errors.
- `DbLive-PersistedTest` is not affected by the checksum change: the Testcontainers container is created for each test run (`WithReuse` is commented out) and disposed after it. Only local `DbLive_DemoMSSQL` databases on localhost (`DemoMSSQL/Program.cs`, `LocalDeployTests`) have the old checksum.
- No undo settings file: `drop index ... with (online = on)` applies to clustered indexes only; dropping a nonclustered index is a metadata operation and runs in a transaction without trouble. `004.undo.sql` already uses `drop index if exists`.
- The typo in `IDX_OrderItmes_ProductId` stays: renaming the index would make databases that already have 004 differ from the files, so `RepairMigrationChecksums` would no longer be correct, and the undo would not drop the old index.

## Result
- `004.migration.sql`: each `create index` runs only if the index does not exist yet; a comment explains why.
- `docs/examples/index.md`: the 004 entry says the script is re-runnable and why.
- Demo tests (17) and `DbLive.MSSQL.Tests` (27) pass.
- Databases deployed with the old 004 need one deployment with `RepairMigrationChecksums = true`.
