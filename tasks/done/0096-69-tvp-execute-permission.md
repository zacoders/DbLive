---
type: bug
priority: medium
created: 2026-11-09
closed: 2026-11-14
---
# The table type of self-migration 13 may need EXECUTE for the deployment login (SQL Server)

## Release note
None. The table type came with 0096-44 and was never released: DbLive 2.0 deploys with the same SQL Server permissions as 1.5 (`db_ddladmin`, `db_datareader` and `db_datawriter`), which the docs now list.

## Problem
Since 0096-44, `MarkCodeAsVerifiedAsync` and `MarkMissingCodeAsRemovedAsync` pass the paths as a table-valued parameter of the type `dblive.tvp_unique_string_list` (self-migration 13). To use a user-defined table type as a parameter, a login needs the `EXECUTE` permission on the type. The owner of the schema `dblive` and the members of `db_owner` have it. `db_ddladmin`, `db_datareader` and `db_datawriter` do not grant it.

Scenario (verified, with a correction, see Notes): an administrator created the schema `dblive` (owned by `dbo`), and the CI login has `db_ddladmin`, `db_datareader` and `db_datawriter`. Self-migration 13 runs under the CI login, and the type belongs to the owner of the schema. Every deployment then fails in the code phase with "The EXECUTE permission was denied on the object 'tvp_unique_string_list'". DbLive 1.5 deployed with this login.

If it is real, it fails every deployment for such a login, so it blocks the release. Most deployment logins are `db_owner`, which is not affected.

The docs do not say which permissions the deployment login needs.

Found by the code review of 2026-11-09 (finding 5).

## Where
- `src/DbLive.MSSQL/SQL/13.tvp-unique-string-list.sql`
- `src/DbLive.MSSQL/MsSqlDA.cs:505-546` (`ToPathList`, `MarkCodeAsVerifiedAsync`, `MarkMissingCodeAsRemovedAsync`)

## Fix
- [x] Reproduce first, on SQL Server with a login that has `db_ddladmin`, `db_datareader` and `db_datawriter` and does not own `dblive`.
- [x] If it is real, which fix?
  - `grant execute on type::dblive.tvp_unique_string_list to public` in the self-migration. The type is only a shape, so the grant exposes nothing. The login that runs the self-migration must be allowed to grant it.
  - No user-defined type: the paths as one `nvarchar(max)` JSON parameter read with `openjson` (compatibility level 130, SQL Server 2016), or a temporary table.
  - Document the permissions the deployment login needs.

  2026-11-14, the maintainer: xml, read into a temporary table with an index, then the query; self-migration 13 is deleted (Notes).

2.0 is not released, so self-migration 13 can still be changed in place. Only databases deployed from this branch already have the type. 0096-70 changes the same type: decide both together.

## Docs to update
- [x] `docs/llms-full.txt` §15 (`dblive` schema): the permissions of the deployment login; the table type is gone
- [x] `docs/deployment/index.md`: "Production" (the deployer's login), "Bookkeeping tables" (the table type is gone)

## Done when
- [x] Reproduced or ruled out, with the permissions of the login written in Notes
- [x] If real: fixed, with an integration test under a login that is not `db_owner`

## Notes
- Reproduced (verified, SQL Server 2025, `mcr.microsoft.com/mssql/server:2025-latest`): the database deployed first by `sa`, so the schema `dblive` and the type belong to `dbo`; then a deployment by a login whose user has `db_ddladmin`, `db_datareader` and `db_datawriter` fails with "The EXECUTE permission was denied on the object 'tvp_unique_string_list', database '...', schema 'dblive'" in `MarkCodeAsVerifiedAsync`, the first use of the type in the code phase. `MarkMissingCodeAsRemovedAsync` runs on every deployment with `DeployCode`, so every such deployment fails.
- The scenario of the Problem is not quite the one that happens: self-migration 01 runs `create schema dblive` without a check, so a schema created by an administrator beforehand fails the first deployment. The schema belongs to `dbo` when a `db_owner` login (or `sa`) deployed the database first, with 2.0 or with 1.5, and a login with the three roles deploys it later.
- Verified: a schema created by a login with `db_ddladmin` belongs to the user of that login. A login that deploys an empty database itself owns `dblive` and was not affected.
- Verified: a login with `db_ddladmin` can create a type in a schema of `dbo`, but the type belongs to `dbo`, and the login can neither use it (error 229) nor grant on it (error 15151, "Cannot find the type ..., because it does not exist or you do not have permission"). So a `grant` in self-migration 13 helps only when `db_owner` or the owner of `dblive` runs self-migration 13; the upgrade of a 1.5 database by the CI login would fail in the self-migration.
- Verified: `openjson` and `string_split` fail with "Invalid object name" at compatibility level 120, also on SQL Server 2025. JSON would add a requirement on the database.
- 2026-11-14, the maintainer: xml, read first into a temporary table with an index, and then the main query: the plan of a query over the nodes of an xml depends on a guess of their number. Delete self-migration 13. This reverses "no xml and no temporary table" of 0096-44 (2026-11-07), decided before the permission was known. Neither needs a permission or an object in the database, and both work with any compatibility level.
- The temporary table `#paths` needs `collate database_default`: it takes the collation of tempdb. Verified: without it the test of 0096-70 (a database with `Latin1_General_CI_AI` on a server with `SQL_Latin1_General_CP1_CI_AS`) fails with "Cannot resolve the collation conflict between "Latin1_General_CI_AI" and "SQL_Latin1_General_CP1_CI_AS" in the equal to operation".
- The index of `#paths` is clustered and not unique: paths that differ only by accent or width are equal for some collations (0096-70).
- The login of the test uses pooled connections. Verified: with `Pooling = false` every connection in a `TransactionScope` of DbLive is a new one, and the self-deployment fails with "Implicit distributed transactions have not been enabled". The test kills the sessions of the login before `drop login` instead of clearing the pools, which would also clear the pools of the tests running in parallel.
- `MinCompatibleVersion` stays: DbLive 1.5 never used the type. The snapshot of `DbLiveTablesSnapshotTests` only loses self-migration 13.
- Databases deployed from this branch keep the type and `dblive.version.version = 13`. DbLive logs that a newer DbLive applied self-migrations it does not have and goes on; a future self-migration 13 would be skipped on them, so recreate them.

## Result
- `MsSqlDA.MarkCodeAsVerifiedAsync` and `MarkMissingCodeAsRemovedAsync` pass the paths as one xml parameter, read it into the temporary table `#paths ( value nvarchar(450) collate database_default )` with a clustered index that is not unique, and join / `not exists` against it. No user-defined type, so the login needs no `EXECUTE`.
- Self-migration `13.tvp-unique-string-list.sql` is deleted; the snapshot of `DbLiveTablesSnapshotTests` updated.
- Permissions, verified by the tests: `db_ddladmin`, `db_datareader` and `db_datawriter` deploy migrations, code, the breaking phase and BeforeDeploy/AfterDeploy, also when `dblive` belongs to `dbo`. Documented in `docs/llms-full.txt` §15 and in "Production" of `docs/deployment/index.md`; the table type is removed from both.
- Tests: `DeploymentLoginPermissionsTests` (SQL Server): a database deployed by `sa`, then deployed by the login with `Default` and `Breaking` (a code file skipped by its hash, one removed, one new, a breaking script of a migration and of the Code folder, BeforeDeploy and AfterDeploy); an empty database deployed twice by the login. The first failed before the fix with the error of the Problem.
