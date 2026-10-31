---
type: chore
priority: low
created: 2026-10-31
---
# DemoMSSQL 004 runs without a transaction but is not re-runnable

## Problem
`src/Demo/DemoMSSQL/Scripts/Migrations/004.migration.sql` runs with `"TransactionWrapLevel": "none"` (`004.migration.settings.json`) and creates two indexes with plain `create index`. The docs (0096-13) say a script without a transaction must be re-runnable, because a failed script runs again from the start on the next deployment. If the second index fails, the rerun fails on the first one ("already exists"). The demo is the example users copy.

## Where
`src/Demo/DemoMSSQL/Scripts/Migrations/004.migration.sql`, `004.undo.sql` (the undo comment describes the same risk).

## Fix
- [ ] Wrap each `create index` in `if not exists (select * from sys.indexes where name = ... and object_id = object_id(...))`?
- [ ] Editing an applied migration changes its checksum: databases that already have 004 (the persisted test database `DbLive-PersistedTest`, local demo databases) fail with `MigrationChecksumMismatchException`. Acceptable for a demo with a note, or deploy them once with `RepairMigrationChecksums = true`?
- [ ] Consider an undo settings file for 004 if the undo drops the indexes online.

## Done when
- [ ] Demo 004 is re-runnable, demo tests green
