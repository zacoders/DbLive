---
type: bug
priority: high
created: 2026-11-09
---
# The self-deployment changes the `dblive` tables before the project is checked

## Problem
`DbLive.DeployAsync` runs the self-deployment (the self-migrations of the `dblive` tables) before it reads the project. The checks of the project run later, inside `DbLiveDeployer`:

- the read of the files: `FilesOutsideProjectFoldersException`, `ScriptPathsDifferOnlyByCaseException`, `BreakingScriptMatchesTestPatternException`;
- the names of migration files: `UndoNotSupportedException`, `ObsoleteMigrationSettingsFileException`, `InvalidMigrationVersionException`;
- the settings files of migration items: `InvalidMigrationItemSettingsException`;
- the transaction settings of `settings.json`: `TransactionSettingsValidator`.

So a deployment that fails on an error of the project has already changed the `dblive` tables.

It matters most on the first deployment after the upgrade to 2.0. The self-migrations added since 1.5.0 (07 to 13 on SQL Server, 04 to 09 on PostgreSQL) cannot be undone: they delete the undo rows, drop `dblive.migration.content`, rename the `settings` rows to `migration.settings`, and write `min_dblive_lib_version = 2.0.0`. The errors above are the ones an upgrade meets: an undo script left in the project, an old `<version>.settings.json`, the key `MigrationTimeout`, a `README.md` in `Scripts`.

Scenario (from the code, not verified by a test): the packages of a branch are updated to 2.0, and the project still has `Migrations/001.undo.sql`. The deployment to a shared dev or staging database fails with `UndoNotSupportedException`. Nothing of the project is deployed, but the database is now in the format of 2.0. The pipeline of `main`, still on 1.5, fails on every deployment from now on: 1.5 writes the column `content` (`Invalid column name 'content'`) and cannot read `migration.settings` or `removed`. The docs say not to deploy a database that DbLive 2.0 has deployed with an older version (§18.8, the release note of 0096-40); nobody expects a failed deployment to count.

The same holds for every later release that raises `MinCompatibleVersion`: a deployment that fails on its project raises the version anyway, and the previous version stops with `DatabaseRequiresNewerDbLiveException`.

The comment in `DbLiveDeployer` "an older build fails here, before anything is changed" is wrong for the same reason: the self-deployment has already run.

The ProjectId check moved before the self-deployment for the same reason (0096-52).

Found by the code review of 2026-11-09 (finding 1).

## Where
- `src/DbLive/DbLive.cs:36-46` (`DeployAsync`: `ProjectIdValidator.ValidateAsync`, `_selfDeployer.DeployAsync`, then `_deployer.DeployAsync`)
- `src/DbLive/Deployers/DbLiveDeployer.cs:24-34` (`TransactionSettingsValidator`, `AppliedMigrationsValidator`, the comment)
- `src/DbLive/Project/DbLiveProject.cs:124` (`GetMigrationsAsync`), `:184` (settings files of items), `:316` (`ReadFiles`)

## Fix
Read and check the project before the self-deployment, next to the ProjectId check: `settings.json`, `TransactionSettingsValidator`, and `GetMigrationsAsync()`, which runs the checks of `ReadFiles` and parses the names of migration files and their settings files. The project caches what it reads (0096-44), so the deployment reads nothing twice.

- [ ] Which reads go before the self-deployment: only `GetMigrationsAsync()`, or also the code (`GetCodeGroupsAsync()` reads every code file, so it finds a file that cannot be read) and the BeforeDeploy/AfterDeploy scripts?
- [ ] Before `RecreateDatabase` and `CreateDbIfNotExists` as well, so that a project error leaves the server as it was?

0096-72 (the isolation level of an item settings file) adds to the same check.

## Docs to update
- `docs/llms-full.txt` §12 (order of `DeployAsync`), §18.8 (a failed deployment does not change the `dblive` tables)
- `docs/deployment/index.md` (the list of steps, "The DbLive version a database requires")

## Done when
- [ ] Fixed, with a unit test: an error of the project fails the deployment before the self-deployment
- [ ] An integration test on SQL Server and PostgreSQL: a project with an undo script fails, and the self-migration version in `dblive.version` does not change
- [ ] The comment in `DbLiveDeployer` corrected
- [ ] Docs updated
