---
type: bug
priority: low
created: 2026-11-24
---
# SQL Server: the first self-migration fails when the schema `dblive` exists

## Problem
`src/DbLive.MSSQL/SQL/01.migrations.sql:1` is `create schema dblive;`. When the schema exists and the `dblive` tables do not, the first deployment fails with Msg 2714 in the self-deployment. That happens when an administrator creates the schema for a deployment login that has only `db_ddladmin`, `db_datareader` and `db_datawriter` (`docs/llms-full.txt` §15, line 1032: "also when the schema `dblive` belongs to another user, such as `dbo`"), for example `create schema dblive authorization dbo`. The PostgreSQL self-migration has `create schema if not exists dblive;` (`src/DbLive.PostgreSQL/SQL/01.migration.sql:2`). From the code, not run.

## Where
- `src/DbLive.MSSQL/SQL/01.migrations.sql:1`

## Fix
`if schema_id('dblive') is null exec('create schema dblive');` (`create schema` must be alone in its batch). A change of an applied self-migration changes nothing for a database that has it: the self-deployer runs a self-migration once, by its number.

- [ ] Also the `create table` of the same file, for a schema with some tables? No: rule 11, nothing else is created in `dblive`.

## Docs to update
- None, unless §15 says when the schema may be created by an administrator.

## Done when
- [ ] Fixed, with a test: a database with an empty schema `dblive` deploys

## Notes
- Found by the code review of 2026-11-24.
