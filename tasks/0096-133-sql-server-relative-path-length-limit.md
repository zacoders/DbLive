---
type: docs
priority: low
created: 2026-11-23
---
# SQL Server: a relative path longer than about 450 characters fails after its file ran

## Problem
`relative_path nvarchar(4000)` is the clustered primary key of `dblive.code` and `dblive.unit_test_run` (`src/DbLive.MSSQL/SQL/02.code.sql`, `03.unit-tests.sql`) and part of the key of `dblive.folder_items` together with `folder_type` (`04.folder-items.sql`). A clustered key holds at most 900 bytes, so a path longer than 450 characters (about 434 in `folder_items`) cannot be stored. The comment at `src/DbLive.MSSQL/MsSqlDA.cs:855` acknowledges the limit.

A longer path makes `SaveCodeItemAsync` fail with Msg 1946 after the file has run; the file counts as failed, is retried in every round and ends in `CodeDeploymentAggregateException`, with an error that does not say why. PostgreSQL has no such limit. Rare, and not documented. Found by reading the code, not reproduced.

## Fix
- [ ] Document the limit in §16, or fail the project load on SQL Server for a longer path, with the path in the message?

## Docs to update
- `docs/llms-full.txt` §15 or §16

## Done when
- [ ] Docs updated (and the check added, if decided)

## Notes
- 2026-11-23: 0096-141 dropped `dblive.unit_test_run`; the limit stays for `dblive.code` and `dblive.folder_items`.
