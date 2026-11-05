---
type: chore
priority: high
created: 2026-11-06
breaking: true
---
# Remove undo scripts and the database downgrade

## Problem
Undo scripts and the downgrade (`AllowDatabaseDowngrade`) cost more than they give:

- They do not make a rollback safe. Before the breaking phase the database needs no rollback: migrations only add, and the previous application runs on the new schema (expand/contract, `docs/llms-full.txt` §18.10 step 4), so a rollback is a rollback of the application. After the breaking phase an undo restores the structure, not the data the breaking scripts dropped; and an undo of a migration drops the data written since it ran.
- Users write and keep an undo script for every migration, and in production it almost never runs.
- The mechanism keeps producing edge cases: three bugs of the code review of 2026-11-05 are about a rollback (0096-40, 0096-41, 0096-42). A rollback with an older build also means that an older DbLive must read the `dblive` tables of a newer one, which would turn every future self-migration into a contract with an undo script of its own (0096-40).
- It is not a reason to choose DbLive: DbUp and grate have no undo, Flyway has it only in its paid editions, and the common practice for production is to roll forward.

## Decision
2026-11-06, maintainer: remove undo scripts and the downgrade before 2.0 is released. The maintainer does not use them; they are unsafe, hard to maintain and not a killer feature. 2.0 is not released and the library has no users, so the removal costs nothing now; after the release it would be a breaking change of a major version. Undo can come back as a new feature in a minor version if there is real demand.

How a release is rolled back after this task (the new §18.8):

- Before the breaking phase: roll back the application. The database stays: it serves both versions.
- A bug in a code object: revert the file in git and deploy a new release. Code is deployed by state, so the old version of the object comes back.
- A wrong migration: fix it with a new migration.
- The breaking phase is the point of no return: run it when the release is confirmed, and never for a release whose application was rolled back (it drops what the previous application uses, and no guard can see it: 0096-41 Notes). After it, fix forward or restore a backup.

2026-11-06, maintainer, on the open questions of this task (the proposals of the discussion accepted):
- Deploying a build that does not have every applied version fails with a renamed exception, `DatabaseAheadOfProjectException`: "downgrade" no longer exists.
- An undo file left in a project fails with an exception of its own, not with the generic `UnknownMigrationItemTypeException`.
- `dblive.migration.content` is dropped by a self-migration.
- Related tasks: 0096-42 and `rejected/undo-test-data-round-trip.md` rejected, 0096-40 reduced to the guard, 0096-41 rechecked, the undo part of `ideas/migration-step-tests.md` removed; the heuristic check of expand/contract is a new idea, `ideas/migration-breaking-change-check.md`.

## Where
Code:
- `src/DbLive/Deployers/Migrations/`: `DowngradeDeployer.cs`, `IDowngradeDeployer.cs`, `DowngradeImpossibleException.cs`, `DowngradeNotAllowedException.cs`; the undo branches of `MigrationVersionDeployer.cs`, `MigrationItemDeployer.cs`, `IMigrationItemDeployer.cs`, `MigrationChecksumValidator.cs`, `MigrationsSaver.cs` (it stores the content of undo items only)
- `src/DbLive/DeployParameters.cs` (`AllowDatabaseDowngrade`), `src/DbLive/Deployers/DbLiveDeployer.cs`, `src/DbLive/Bootstrapper.cs`
- `src/DbLive/Project/`: `MigrationItemType.cs` (`Undo`, `UndoSettings`), `MigrationItemTypeMapper.cs`, `MigrationFileNameParser.cs` (`undo`, `u`)
- `src/DbLive/Common/Settings/` (`DbLiveSettings.cs`, `MigrationItemSettings.cs`, `SettingsTools.cs`), `src/DbLive/Common/UndoTestMode.cs`
- `src/DbLive/Adapter/IDbLiveDA.cs` and its implementations in the providers (`reverted` status, undo content)
- `src/DbLive/Schema/SchemaCheck.cs` (the `Undo` check), `src/DbLive.xunit/Deploy/DeploymentTestFixture.cs` (the undo cycle)
- `src/DbLive.MSSQL/SQL/`, `src/DbLive.PostgreSQL/SQL/`: a new self-migration (`11` / `08`)

Scripts and samples:
- `src/DbLive.Templates/templates/dblive/scripts/{mssql,postgresql}/MyDb/Scripts/Migrations/MIGRATION_VERSION.undo.sql`, `src/DbLive.Templates/templates/dblive/MyDb.Tests/DeploymentTests.cs` (`ReleaseDeploysAndRollsBack`)
- `src/Demo/DemoMSSQL/Scripts/Migrations` (undo scripts, also in `Old`), `settings.json` of `DemoMSSQL` and `AdventureWorks.Database`

Tests: `src/DbLive.Tests` (`Deployers/Migrations`, `Project`, `Deploy`), `src/DbLive.MSSQL.Tests`, `src/DbLive.PostgreSQL.Tests`, `src/DbLive.Tests.Common`, the test projects of the demos.

## Fix
- [ ] Remove undo items (`<version>.undo.sql`, `<version>.u.sql`, `<version>.undo.settings.json`), the downgrade, `AllowDatabaseDowngrade`, the `reverted` status and `UndoTestMode`.
- [ ] `[SqlDeployFact]` without the undo cycle: (1) `Default`, then the SQL tests; (2) `Breaking`, where the breaking script of every migration runs twice and its second run must not change the schema (`BreakingSecondRun`: a failed breaking script with `TransactionWrapLevel: None` runs again on the next deployment; today only versions with an undo script are checked), then the SQL tests; (3) `Breaking` once more (`BreakingSecondDeployment`). The `Undo` check goes. Rename the test of the template: `ReleaseDeploysAndRollsBack` → `ReleaseDeploys`.
- [ ] Keep the `removed` status of `dblive.code`: a deleted code file that a later release brings back (`git revert`) must run again, because the breaking phase may have dropped its object. Reword §6 and §6.1 without "rollback".
- [ ] A database with applied versions the build does not have: the deployment fails before any change with `DatabaseAheadOfProjectException` (renamed from `DowngradeNotAllowedException`). The message lists the versions and names both causes: an older build is deployed (DbLive does not roll back a database: roll back the application, §18.8), or migration files were deleted from the project (restore them).
- [ ] An undo file left in a project (`005.undo.sql`, `005.u.sql`, `005.undo.settings.json`): the deployment fails before any change with an exception of its own (`UndoNotSupportedException`?) that names the file and says that undo scripts are not supported: delete the file, §18.8.
- [ ] Self-migration (SQL Server `11`, PostgreSQL `08`): delete the rows with `item_type` `undo` and `undo.settings`; delete the rows with status `reverted` (their version was rolled back, so it is not applied: the saver adds the rows again and the migration runs if the build has it); drop the column `dblive.migration.content`, which holds only undo content.
- [ ] Stale `breaking` and `breaking.settings` rows of a file deleted from the project (from 0096-42): does the breaking phase or the checksum validation read them? Check while changing `MigrationsSaver`; a task of its own if they matter.
- [x] Related tasks (2026-11-06): `0096-42` and `rejected/undo-test-data-round-trip.md` rejected; `0096-40` reduced to the guard against an older DbLive and renamed to `0096-40-older-dblive-on-newer-database.md`; `0096-41` rechecked (Notes there: the scenario stays, and a rollback of the application adds a case no guard sees); the undo part of `ideas/migration-step-tests.md` removed; `0096-21`, `0096-44`, `0096-documentation`, `0099-unit-testing-improvements` and `tasks/README.md` updated.

## Docs to update
- `docs/llms-full.txt`: every section that mentions undo or downgrade: §0, §1, §5.1 to §5.5 (§5.5 goes), §6, §6.1, §7, §8.2, §9.2, §9.3, §10, §11.3, §12, §13.3, §15, §16, §17, §18.1, §18.3 to §18.5, §18.7, §18.8 (rewritten: how to roll back without undo), §18.10
- `docs/llms.txt`, `README.md`, `src/DbLive/README.md`, `src/DbLive.xunit/README.md`, `src/DbLive.Templates/README.md`
- `docs/deployment/` (`migrations.md`, `breaking-changes.md`, `code.md`, `index.md`), `docs/project/` (`configuration.md`, `structure.md`), `docs/unit-testing/` (`deployment-tests.md`, `index.md`, `stored-proc-and-unit-test.md`), `docs/examples/` (`index.md`, `table-split.md`), `docs/features/index.md`, `docs/index.md`, `docs/gettingstarted.md`, `docs/database-as-service.md`

## Done when
- [ ] Undo and the downgrade are removed from the code, the tests, the template and the demos; the build and all tests pass
- [ ] `[SqlDeployFact]` runs three deployments without the undo cycle and checks the second run of every migration breaking script
- [ ] Deploying an older build fails before any change with a clear message, with a unit test
- [ ] The docs describe a rollback without undo (§18.8) and mention undo nowhere else
- [ ] Release note (breaking): what is removed, and what to do with undo files and `AllowDatabaseDowngrade`

## Notes
- 2026-11-06, discussion of 0096-40 (a rollback with a build of an older DbLive). Options considered before this decision: an older DbLive reads the `dblive` tables of a newer one (not feasible: old code cannot know future values); a rollback with the newest DbLive and the scripts of the older release (`SetScriptsFolder`; changes the user's rollback pipeline); undo scripts for DbLive's own self-migrations, stored in the database and run by the older DbLive (works, but makes every self-migration a permanent contract). Removing undo makes all three unnecessary.
- A rollback now relies on expand/contract: a release must not break the previous application before its breaking phase. Nothing checks it; a heuristic check on the schema compare is the idea `tasks/ideas/migration-breaking-change-check.md`.
