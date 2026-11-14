---
type: bug
priority: medium
created: 2026-11-05
closed: 2026-11-07
---
# An older DbLive deploys a database that a newer DbLive has deployed

## Release note
**An older DbLive stops on a database that a newer DbLive has changed.** The database keeps the oldest DbLive version that can deploy it, in the new column `dblive.version.min_dblive_lib_version`, which the first deployment adds. A deployment or `RunAllTestsAsync()` with an older DbLive fails with `DatabaseRequiresNewerDbLiveException` (namespace `DbLive.SelfDeployer`) before it changes anything, and the message names the version to update the packages to. Before, it failed with an unclear error, or wrote rows in an old format next to the new ones. A release after which older versions can no longer deploy the database says so in its notes: to roll back such a release, roll back the application and keep the database. DbLive versions before 2.0.0 do not check the version: do not deploy a database that DbLive 2.0 has deployed with them. Nothing to change when you upgrade.

## Problem
A newer DbLive can store in the `dblive` tables what an older one cannot read or understands differently. The change can come with a self-migration (a dropped or renamed column, converted data) or without one, from the C# code alone (a new value in a stored column, a changed path format). An older DbLive that meets such a database fails with an error that does not say why (a Dapper enum parse, an SQL error), or, worse, goes on and writes rows in the old format next to the new ones.

Nothing stops it today. `DbLiveSelfDeployer` keeps only the self-migrations above the stored `dblive.version`; when the stored version is above its own last self-migration, the list is empty and the deployment goes on. A check of the self-migration version would not be enough either: it misses a change without a self-migration, and it blocks an additive self-migration that an older DbLive can live with.

After 0096-46 there is no downgrade, but an older DbLive still meets such a database when an old artifact is deployed: by mistake, or as a rollback after a release that only upgraded DbLive. Such a release has no migrations of its own, so the check of 0096-46 (the database has applied versions the build does not have) does not stop it.

## Where
- `src/DbLive/SelfDeployer/DbLiveSelfDeployer.cs:25` (reads `dblive.version` and filters the self-migrations)
- `src/DbLive/Testing/DbLiveTester.cs:26` (`RunAllTestsAsync` writes `dblive.unit_test_run` without a deployment)
- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs` (reading and writing `dblive.version`)
- `.github/scripts/release-notes.cs` (the release check)

## Fix
A minimum compatible DbLive version, stored in the database as a semver version.

- [x] A constant in the code, `MinCompatibleVersion = "2.0.0"`: a database this DbLive has deployed can be deployed by DbLive of this version or newer. It is raised in the change after which an older DbLive misreads or miswrites the `dblive` tables, to the version of the release that ships the change.
- [x] Self-migrations `src/DbLive.MSSQL/SQL/12.min-dblive-lib-version.sql` and `src/DbLive.PostgreSQL/SQL/09.min-dblive-lib-version.sql`: column `dblive.version.min_dblive_lib_version`, null when there is no requirement; re-runnable.
- [x] `IDbLiveDA`: read and write the requirement, in both providers. Reading returns null when the column does not exist yet (a database before the self-migration above), so the self-deployer does not need the provider's self-migration number.
- [x] `DbLiveSelfDeployer`, inside its transaction, before any change:
  - the stored requirement above `MinCompatibleVersion` → `DatabaseRequiresNewerDbLiveException` (`RequiredVersion`, `MinCompatibleVersion`), with a message such as: "The database requires DbLive 2.3.0 or newer: a newer DbLive changed the `dblive` tables in a way older versions cannot read. This is DbLive 2.1.0. Update the DbLive packages to 2.3.0 or newer.";
  - the self-migrations are applied as now; a stored self-migration version above the last one of this DbLive is not an error by itself, only a log line;
  - the larger of the stored requirement and `MinCompatibleVersion` is written; it is never lowered.
- [x] The requirement is compared with `MinCompatibleVersion` of this build, not with its assembly version: the assembly version is `0.0.0-placeholder` in a local build and a prerelease on a branch. The assembly version only goes into the message.
- [x] `DbLiveTester.RunAllTestsAsync` checks the requirement the same way after the `DatabaseNotDeployedException` check, and does not write it. One piece of code for both. `RunTestAsync` (`[SqlFact]`) is not checked: the fixture deploys with the same build first.
- [x] `release-notes.cs`: when `MinCompatibleVersion` changed since the previous tag, it must equal the version of the release. Checked on a pull request (`--check`, runs again when the title is edited) and on `main` before anything is published, which also catches two PRs that raised it at the same time.
- [x] A snapshot test of what an older DbLive depends on: the self-migrations of both providers and the values of the enums stored in `dblive` tables. A new self-migration or value fails it with: decide whether an older DbLive still works, then raise `MinCompatibleVersion` or update the snapshot. It does not catch a change of behavior in C# alone; the rule in Development.md covers that.
- [x] Development.md: when to raise `MinCompatibleVersion` (a new or changed value or meaning of a stored column, a dropped or renamed column, a NOT NULL column without a default) and when not (a nullable column or a table the older code ignores); a release that raises it says in its release note that older DbLive versions can no longer deploy the database.

## Docs to update
- `docs/llms-full.txt` §15 (`dblive.version.min_dblive_lib_version` and the check), §17 (`DatabaseRequiresNewerDbLiveException`), §18.8 (the previous build fails after a release that raised the minimum version: roll back the application, keep the database)
- `docs/deployment/index.md` (the `dblive` schema)
- `docs/versioning.md` ("What the policy covers", the `dblive` schema: a release that raises the minimum DbLive version says so in its notes)
- `Development.md` (when to raise `MinCompatibleVersion`, the release check)
- `AGENTS.md` (a line that points to the rule in Development.md)
- `docs/deployment/migrations.md` ("Roll back a release"), `docs/unit-testing/index.md` (the checks of `RunAllTestsAsync`), `.github/pull_request_template.md` (a checklist item); found while updating the pages above

## Done when
- [x] Implemented, with unit tests (self-deployer, tester, snapshot)
- [x] Integration tests on SQL Server and PostgreSQL: a requirement above `MinCompatibleVersion` fails the deployment and changes nothing
- [x] The release check of `MinCompatibleVersion` in `release-notes.cs`
- [x] Docs updated

## Notes
Found in the code review of 2026-11-05 (#96), first as "after a deployment with DbLive 2.0, a build with DbLive 1.5.0 cannot deploy the database": the self-migrations 07 to 10 (SQL Server) and 04 to 07 (PostgreSQL) write values that 1.5.0 cannot read (`migration.settings`, `breaking.settings`, `undo.settings`, the code status `removed`, paths with `/` in lower case).

2026-11-06, maintainer: versions before 2.0 are not considered, the library has no users; undo scripts and the downgrade are removed (0096-46). The task is reduced to the guard. The options considered for a rollback across a DbLive upgrade are in the Notes of 0096-46.

2026-11-07, maintainer: compatibility can break without a self-migration, so the guard is a minimum compatible DbLive version instead of a check of the self-migration version. The requirement is a semver version, so the message names the DbLive version to take. A release that raises it gets a line in its release note and is not `breaking: true`: an upgrade needs no change on the user's side, only a rollback across it stops working. `RunAllTestsAsync` checks the requirement too.

- Considered and dropped: failing when the stored self-migration version is above the last one of this DbLive (misses a change in C# alone and blocks an additive self-migration); an integer compatibility level (the user would have to look up which DbLive version has it).
- The guard must ship in 2.0: it runs in the older DbLive, so a version without it is never protected. Added in 2.1, it would protect 2.1 on a database of 2.2, but not 2.0 on a database of 2.1.
- The requirement is written at the start of the deployment, in the self-deployer transaction, not after a successful deployment: the self-migrations cannot be undone anyway, and a deployment that fails later may already have written rows in the new format.
- Why comparing the constant is enough: the constant of a build is the version of the latest incompatible change it contains. A requirement above it means a DbLive with a change this build lacks has deployed the database; a requirement at or below it means the build has every such change.
- The assembly versions, from `obj/*/DbLive.AssemblyInfo.cs`: a local Debug build has `0.0.0-placeholder`, a local Release build of this branch `2.0.0-undo.1`.

2026-11-07, implementation:

- Verified in a scratch repository: `git log -G ... <range> -- <file>` does not see the second of two pull requests that raised the constant to the same version, because the merge leaves the file as it was and git simplifies the history; `--full-history` finds its commit. Also verified there: a matching release passes, a wrong version and the second pull request fail, an unrelated release after a raise passes.
- The release check counts any commit since the previous tag that changed the declaration line, not only the difference to the tag: that is what catches the second pull request. It runs only when the version of the release is known (`--check` with the PR title, `--version` on `main`); a local preview without them skips it.
- The check is one static method, `DbLiveCompatibility.Check`, for the self-deployer and `RunAllTestsAsync`, not a DI service.
- The self-deployer reads `DbLiveInstalledAsync`, the requirement and the self-migration version inside its Serializable transaction now (before, the version was read outside). It writes the requirement only when the value changes, so a deployment that changes nothing does not update `dblive.version`.
- Reading a column that may not exist: SQL Server runs the `select` through `sp_executesql` when `col_length` finds the column (a batch that names a missing column does not compile); PostgreSQL reads `to_jsonb(v) ->> 'min_dblive_lib_version'`, which is null without the column. Verified by the integration test that drops the column and lowers the self-migration version.
- The message of the exception also says how to roll back a release, because a rollback is the main way to get there.

2026-11-07, maintainer: "DbLive version" meant three things: the number of the last self-migration (`dblive.version.version`), the minimum release version of the library and the version of the running assembly. The names say which one now: the column `min_dblive_lib_version` (renamed before any release, so no self-migration), `IDbLiveDA.GetSelfMigrationVersionAsync` / `SetSelfMigrationVersionAsync` and `GetMinDbLiveLibVersionAsync` / `SetMinDbLiveLibVersionAsync`, `DbLiveCompatibility.GetRunningDbLiveLibVersion`. The column `version` keeps its name: it is released since 1.x, and the self-deployer reads it before it knows whether a renaming self-migration ran.

2026-11-07, maintainer: the first version was too complicated. Versions are three numbers, so the code works with `System.Version`, not with strings: `MinCompatibleVersion` is `new(2, 0, 0)`, the data access reads and writes `Version` (a `varchar` `major.minor.patch` in the column). After the check the stored value is at most `MinCompatibleVersion`, so the self-deployer writes `MinCompatibleVersion` when the stored value is missing or lower; the "larger of the two", the parsing with a regex, a blank value as no requirement and an unreadable value as a higher one are gone. A value that is not a version can come only from a hand edit and fails in `Version.Parse`. The release check reads `new(<major>, <minor>, <patch>)`.

## Result
- `DbLiveCompatibility.MinCompatibleVersion = new(2, 0, 0)` (`src/DbLive/SelfDeployer/DbLiveCompatibility.cs`) and `Check(Version?)`. `DatabaseRequiresNewerDbLiveException` (`RequiredVersion`, `MinCompatibleVersion`, both `Version`; the message names the assembly version without the commit).
- Self-migrations `12.min-dblive-lib-version.sql` (SQL Server) and `09.min-dblive-lib-version.sql` (PostgreSQL): `dblive.version.min_dblive_lib_version varchar(32) null`, re-runnable.
- `IDbLiveDA.GetMinDbLiveLibVersionAsync` (`Version?`, null without the column) and `SetMinDbLiveLibVersionAsync(Version)` in both providers. `GetDbLiveVersionAsync` / `SetDbLiveVersionAsync` renamed to `GetSelfMigrationVersionAsync` / `SetSelfMigrationVersionAsync`.
- `DbLiveSelfDeployer`: inside its transaction checks the requirement before any change, logs a self-migration version above its own instead of failing, applies its self-migrations, writes `MinCompatibleVersion` when the stored value is missing or lower. `DbLiveTester.RunAllTestsAsync` checks after the `DatabaseNotDeployedException` check and writes nothing.
- `release-notes.cs`: when a commit since the previous tag changed `MinCompatibleVersion`, it must equal the version of the release; on the PR check and on `main`.
- Tests: `DbLiveCompatibilityTests`, new cases in `DbLiveSelfDeployerTests` and `DbLiveTesterTests`, the snapshot `DbLiveTablesSnapshotTests`, and `NewerDbLiveDatabaseTests` on SQL Server and PostgreSQL (a requirement above `MinCompatibleVersion` fails `Default`, `Breaking` and `RunAllTestsAsync` and changes nothing; newer self-migrations with an allowing requirement deploy; a database without the column gets it). The whole solution passes, integration tests included.
- Docs: Development.md "Compatibility of the `dblive` tables" (when to raise, the release check), AGENTS.md, `docs/llms-full.txt` §0 rule 9, §8.2, §11.5, §12, §15, §17, §18.8, `docs/deployment/index.md` ("The DbLive version a database requires"), `docs/deployment/migrations.md`, `docs/unit-testing/index.md`, `docs/versioning.md`, the PR template.
