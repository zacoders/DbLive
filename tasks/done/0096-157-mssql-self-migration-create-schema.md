---
type: bug
priority: low
created: 2026-11-24
closed: 2026-11-24
---
# SQL Server: the first self-migration fails when the schema `dblive` exists

## Release note
SQL Server: the first deployment of a database where the schema `dblive` already exists without its tables, because an administrator created it, no longer fails with `There is already an object named 'dblive' in the database.`: DbLive creates its tables in that schema. Nothing to change on upgrade.

## Problem
`src/DbLive.MSSQL/SQL/01.migrations.sql:1` is `create schema dblive;`. DbLive counts itself installed when the table `dblive.version` exists (`MsSqlDA.cs:108`), so a database with the schema `dblive` and without its tables runs the first self-migration, and it fails with Msg 2714. Verified by the new test before the fix: `DbLiveSqlException: There is already an object named 'dblive' in the database.` The PostgreSQL self-migration has `create schema if not exists dblive;` (`src/DbLive.PostgreSQL/SQL/01.migration.sql:2`).

DbLive itself never leaves such a schema: the self-deployment is one transaction (`DbLiveSelfDeployer.cs:34`), so a failed first deployment rolls back `create schema` too. A login with `db_ddladmin` creates the schema itself (`DeploymentLoginPermissionsTests.Login_with_ddladmin_datareader_datawriter_deploys_an_empty_database`), so an administrator does not need to create it for that login. The schema exists without tables only when someone created it by hand: an administrator who prepares the schemas of a database, or a login with narrower permissions than §15 names.

## Where
- `src/DbLive.MSSQL/SQL/01.migrations.sql:1`

## Fix
`if schema_id('dblive') is null exec('create schema dblive');` (`create schema` must be alone in its batch). A change of an applied self-migration changes nothing for a database that has it: the self-deployer runs a self-migration once, by its number, and does not compare its content.

- [x] Also the `create table` of the same file, for a schema with some tables? No: rule 11, nothing else is created in `dblive`.

## Docs to update
- `docs/llms-full.txt` §15 (permissions of the deployment login: an administrator created the empty schema)
- `docs/deployment/index.md` (the row of the deployer's login)

## Done when
- [x] Fixed, with a test: a database with an empty schema `dblive` deploys
- [x] Docs updated

## Notes
- Found by the code review of 2026-11-24.
- The first version of this task said that an administrator creates the schema for a login with only `db_ddladmin`, `db_datareader` and `db_datawriter`. That login creates the schema itself; the sentence of §15 it cited is about a database an administrator deployed, with the tables. The maintainer chose the fix anyway (2026-11-24): one line, and SQL Server behaves like PostgreSQL.
- `DbLiveTablesSnapshotTests` failed on the new content of `01.migrations.sql`. `MinCompatibleVersion` stays: the tables are the same, an older DbLive reads and writes them as before. Only the snapshot is updated.

## Result
- `01.migrations.sql` of SQL Server creates the schema `dblive` only when it does not exist, as the PostgreSQL self-migration does. A database with an empty schema `dblive` gets its tables in it, and the schema keeps its owner.
- Test `DeploymentLoginPermissionsTests.Login_with_ddladmin_datareader_datawriter_deploys_a_database_with_an_empty_dblive_schema`: an administrator creates the database and `create schema dblive authorization dbo`, a login with `db_ddladmin`, `db_datareader` and `db_datawriter` deploys, the code file is applied and the schema still belongs to `dbo`. It fails without the fix with Msg 2714 (verified), the three tests of the class pass with it. `DbLiveTablesSnapshotTests` passes with the new hash of `01.migrations.sql`.
- Docs: `docs/llms-full.txt` §15 and `docs/deployment/index.md` (deployer's login) name an empty schema created by an administrator next to a database an administrator deployed.
