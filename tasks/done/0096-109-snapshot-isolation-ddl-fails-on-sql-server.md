---
type: docs
priority: medium
created: 2026-11-23
closed: 2026-11-23
---
# SQL Server: `Snapshot` isolation fails migrations with `ALTER TABLE` or index DDL

## Release note
SQL Server: the warning that DbLive logs for `"TransactionIsolationLevel": "Snapshot"` in `settings.json` now also says that SQL Server does not allow `ALTER TABLE` and index DDL in a snapshot transaction (Msg 3964), so at the project level `Snapshot` fails each migration in a transaction that alters a table or an index. Set `Snapshot` in the item settings file of a data migration instead. The docs say so too. DbLive behaves as before; nothing to change on upgrade.

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

- [x] Also say in the existing warning of `TransactionSettingsValidator` that `ALTER TABLE` and index DDL fail at this level? Yes (the maintainer, 2026-11-23).

## Docs to update
- `docs/llms-full.txt` §9.2, §9.3, §16
- `docs/project/configuration.md`

## Done when
- [x] Docs updated

## Notes
- Verified on 2026-11-23 on LocalDB 2025 (17.0.4025.3), in a database with `allow_snapshot_isolation on`: after `set transaction isolation level snapshot` without `begin transaction` (autocommit), `alter table dbo.t add c int null` and `create index ix_t_v on dbo.t(v)` succeeded; after `begin transaction`, `alter table` failed with Msg 3964, and `create or alter procedure` succeeded. So a script with `TransactionWrapLevel: None` and `Snapshot` runs its DDL, and the docs say "in a transaction".
- No test checks the text of the warning; the unit tests of `TransactionSettingsValidator` pass.

## Result
- `docs/llms-full.txt` §9.2 (`TransactionIsolationLevel`): SQL Server does not allow `ALTER TABLE` and index DDL in a snapshot transaction (Msg 3964), so `Snapshot` at the project level fails every migration and breaking script in a transaction that alters a table or an index, and a code file with index DDL; set it in the item settings of a data migration. §9.3: `Snapshot` fits a script that reads and changes data; without a transaction the DDL runs. §16, the row `Snapshot` isolation: the same limit, in short.
- `docs/project/configuration.md`: the same in the project setting `TransactionIsolationLevel` (with a link to the item settings) and in the item setting.
- `TransactionSettingsValidator`: the warning for `Snapshot` in `settings.json` on SQL Server also says that `ALTER TABLE` and index DDL fail in a snapshot transaction and to set `Snapshot` in the item settings of a data migration.
- No behavior change; no new tests (the text of a log message).
