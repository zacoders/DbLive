---
type: docs
priority: medium
created: 2026-11-23
---
# SQL Server: `Snapshot` isolation fails migrations with `ALTER TABLE` or index DDL

## Problem
The docs say that `Snapshot` is supported on SQL Server and needs only `ALLOW_SNAPSHOT_ISOLATION ON`. SQL Server does not allow `ALTER TABLE`, `CREATE INDEX`, `ALTER INDEX`, `DROP INDEX`, `CREATE XML INDEX` and partition DDL in a snapshot transaction (Msg 3964). Verified on 2026-11-23 on SQL Server 2025, in a database with `allow_snapshot_isolation on`, after `set transaction isolation level snapshot; begin transaction`:
- `create table` succeeded;
- `alter table dbo.t add c int null` and `create index ix_t_v on dbo.t(v)` failed: "Msg 3964 ... Transaction failed because this DDL statement is not allowed inside a snapshot isolation transaction. Since metadata is not versioned, a metadata change can lead to inconsistency if mixed within snapshot isolation."

So a project-level `"TransactionIsolationLevel": "Snapshot"` fails ordinary migrations, because each runs in a transaction at that level. Code files in their own transaction fail on index DDL too, and with `TransactionWrapLevel: Deployment` every script runs in one snapshot transaction. The only test of `Snapshot` (`src/DbLive.MSSQL.Tests/MsSqlDeployerTests.cs:226-243`) uses `create table` and `insert`.

## Where
- `docs/llms-full.txt` §9.2 (`TransactionIsolationLevel`), §9.3, §16 (row `Snapshot` isolation)
- `docs/project/configuration.md:69` and `:123`
- `src/DbLive/Common/TransactionSettingsValidator.cs:21-27`: the warning about `ALLOW_SNAPSHOT_ISOLATION ON`

## Fix
Document the limit: `Snapshot` fits the item settings of a data migration, not the project level.

- [ ] Also say in the existing warning of `TransactionSettingsValidator` that `ALTER TABLE` and index DDL fail at this level?

## Docs to update
- `docs/llms-full.txt` §9.2, §9.3, §16
- `docs/project/configuration.md`

## Done when
- [ ] Docs updated
