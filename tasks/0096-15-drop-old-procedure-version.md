---
type: feature
priority: medium
created: 2026-10-31
---
# Breaking scripts in the Code folder: drop an old procedure version without a migration

## Problem
When the contract of an interface procedure changes, its file is changed in place and the object is renamed (`MyStoredProc_v1` → `MyStoredProc_v2`). The old version stays in the database for old applications and must be dropped in the breaking phase, after they are gone. Often such a release changes only code: no tables change, so there is no migration and no natural place for `drop proc dbo.MyStoredProc_v1`.

Current behavior (verified by reading the code):
- A breaking script belongs to a migration version (`<version>.breaking.sql`). A version that has a breaking script but no migration file fails when the project is read: `MigrationItemMustExistsException` (`DbLiveProject.GetMigrationsAsync`).
- A breaking script runs only with `DeployBreaking = true`, and only for a version whose migration is already applied (`BreakingChangesDeployer`). `DeployParameters.Breaking` deploys neither migrations nor code, so the breaking deployment is a separate run and the migration was applied by an earlier one.
- `BeforeDeploy` and `AfterDeploy` run on every deployment, whatever the `DeployParameters` (`DbLiveDeployer` calls `FolderDeployer` unconditionally). A drop in `AfterDeploy` runs with `DeployParameters.Default`, before the old application is gone.

Today's workaround (`docs/llms-full.txt` §18.6, `docs/deployment/breaking-changes.md`): add a new version with a trivial migration (`select 1;`) and put the drop into its breaking script. It works, but the migration exists only to carry the breaking script, and the migration list fills with empty versions.

## Decision
Option C, chosen by the maintainer: a breaking script lives in the `Code` folder, next to the code it cleans up.

- `<name>.breaking.sql` next to `<name>.sql`, or `breaking.sql` shared by its folder; short forms `<name>.b.sql` and `b.sql`, as everywhere else in DbLive. The pair is only a naming convention: DbLive does not link a breaking script to a code file, so a script without a matching code file is valid (a procedure retired together with its file).
- Runs in the breaking phase (`DeployBreaking = true`), together with the breaking scripts of migrations.
- No undo scripts for code. A code file has no direction: DbLive cannot tell whether a changed hash is an upgrade or a rollback, so a code undo would also run on an upgrade and drop `_v1` in the normal phase. Tying it to a migration downgrade does not help either: a code-only release has no migration. A rollback restores the code by itself (Plan, step 5).

Rejected:
- A. Keep the workaround: the migration list fills with empty versions.
- B. Allow a version with a breaking script and no migration: still ties the drop of a code object to the migration list, far from the code.
- D. A folder that runs only in the breaking phase: far from the code it cleans up; C covers the same need with `breaking.sql` per folder.

## Plan
1. **Discovery.** In `Code/`, `breaking.sql`, `b.sql`, `*.breaking.sql` and `*.b.sql` are code breaking scripts: excluded from code items and from tests. Order: by relative path, like BeforeDeploy/AfterDeploy. A file that matches both a test pattern and a breaking name fails the deployment with an exception that names the file: with the default `TestFilePatterns` (`test.*.sql`, `t.*.sql`, `*.test.sql`, `*.t.sql`) that is `test.breaking.sql`, `t.b.sql` and the like.
2. **When.** In the breaking phase, before the breaking scripts of migrations: dependents are dropped before what they depend on (on PostgreSQL a view `_v1` blocks the `drop column` of a migration breaking script).
3. **Guard for the whole breaking phase.** The breaking phase (code and migration breaking scripts) runs only when every code file of the project has an `applied` row in `dblive.code` with its current hash, that is, the normal deployment of this build completed. Otherwise the deployment fails with an exception that says how many code files differ and names a few. A failure, not a skip with a warning: a skipped breaking phase leaves the pipeline green, and the operator believes the breaking changes were deployed.
   - In a run that also deploys code (`DeployCode = true`), a failed code file stops the deployment before the breaking phase, and the guard always passes. A project without code files passes too. But `DeployParameters.Breaking` does not deploy code, so in the production breaking run nothing that could fail runs before the breaking phase.
   - Without the guard, the breaking run of N+1 drops `_v1` while application N still uses it when: the `Default` run of N+1 failed and the `Breaking` stage is started anyway; `Breaking` runs before `Default`; the database was rolled back to N and a stale `Breaking` job of N+1 runs.
   - The same hole exists today for migration breaking scripts: `Default` of N+1 applies and commits its migrations (each in its own transaction by default), then a code file fails; a `Breaking` run of N+1 runs the breaking scripts of those migrations, because "the migration is applied" holds, and drops the old column while application N still runs. The guard closes it. Behavior change for migration breaking scripts: release note.
4. **Execution, no state.** Each script is one command without its own transaction, like a code file or an AfterDeploy script (inside the deployment transaction with `TransactionWrapLevel: Deployment`); timeout `CodeItemTimeout`. Every code breaking script runs on every breaking deployment, like AfterDeploy runs on every deployment. The scripts must be idempotent anyway (`drop ... if exists`): on a new database `_v1` never existed. A failure in the middle of a script leaves some old objects dropped and others not: harmless, the next breaking deployment finishes the job. Without state, a rollback and an upgrade again work: deploy N+1 → breaking drops `_v1` → roll back to N (`_v1` is back) → deploy N+1 → the next breaking deployment drops `_v1` again. A hash-based "applied once" would skip it and leave `_v1` forever. Each run is recorded in `dblive.code` (step 7).
5. **A rollback restores the code.** Code deploys by hash per path (`CodeItemDeployer`), and a downgrade runs before the code in the same deployment. Rolling back N+1 → N: `my-proc.sql` has the hash of N again, is executed, and `_v1` is back; `_v2` stays and does not disturb application N. Dropping all code on a downgrade is not an option: DBAs may create objects outside DbLive.
   - Gap: if the old version lived in its own file that N+1 deleted (`my-proc-v1.sql`), its `dblive.code` row stays `applied` with the same hash, so the file returning with N is skipped and `_v1` is not restored.
   - Fix: when code is deployed (`DeployCode = true`), rows whose file is neither a code file nor a code breaking script of the project get the status `removed`. `status` is `varchar(32)` without a check constraint, so the `dblive` schema does not change, and `CodeItemDeployer` already executes a file whose row is not `applied`, so a returning file is executed again. The row keeps a trace of which file disappeared and when.
   - Side effect: without ProjectId, two projects in one database mark each other's rows, and the other project redeploys all its code next time (slower, harmless).
6. **`[SqlDeployFact]`: breaking twice.** Today it deploys everything in one run (`DeployFixture.DeployAsync`). In the cases with breaking, run a second deployment with `DeployParameters.Breaking` after it: a separate run, as in production. It checks that code breaking scripts are idempotent (they run again), that the guard passes after a normal deployment, and that BeforeDeploy/AfterDeploy scripts survive a second run (the bug of 0096-16 would fail it). Migration breaking scripts are applied once and do not run in the second deployment; instead, the undo mode `MigrationBreakingUndoMigration` runs each of them twice: migration → breaking → breaking → undo → migration (`MigrationVersionDeployer`). They must survive it, because a failed breaking script with `TransactionWrapLevel: None` runs again. Limit: on an empty database `_v1` never existed, so both runs see it absent. The first run already fails a `drop` without `if exists`; the second one catches a script whose first run changed something. Dropping an `_v1` that really exists needs the build of the previous release and stays untested.
7. **Record in `dblive.code`.** Each run of a code breaking script writes the row of its file: `status` `applied` or `error` with the `error` text, `content_hash`, `applied_utc` (time of the last run), `execution_time_ms`; `verified_utc` stays empty. One place for every file of `Code/`, and a failed breaking script stays visible with its error (`select relative_path, error from dblive.code where status = 'error'`). `dblive.folder_items` was the other option: it needs no schema change, but it has no `status` and `error` and records only successful runs.
   - A new column `item_type` (`code` / `breaking`, default `code`) tells the rows apart: a self-migration of the `dblive` schema on each engine. Not separate status values (`breaking_applied`): the status would carry two meanings.
   - The guard (step 3) reads only the rows of the project's code files.

## Where
- `src/DbLive/Project/DbLiveProject.cs` (`GetCodeGroupsAsync`: exclude the scripts; test discovery at line 172; a new method for code breaking scripts)
- `src/DbLive/Deployers/DbLiveDeployer.cs:51`, `src/DbLive/Deployers/Migrations/BreakingChangesDeployer.cs` (guard; code breaking scripts before migration ones, or a new deployer called before it)
- `src/DbLive/Deployers/Code/CodeDeployer.cs`, `src/DbLive/Deployers/Code/CodeItemDeployer.cs`, `src/DbLive/Adapter/CodeItemStatus.cs` (status `removed`)
- `src/DbLive/Adapter/IDbLiveDA.cs`, `src/DbLive.MSSQL/MsSqlDA.cs`, `src/DbLive.PostgreSQL/PostgreSqlDA.cs` (guard query, rows of missing files, rows of breaking scripts)
- New self-migrations of the `dblive` schema: `src/DbLive.MSSQL/SQL/10.code-item-type.sql`, `src/DbLive.PostgreSQL/SQL/07.code-item-type.sql` (column `item_type`)
- `src/DbLive.xunit/Deploy/DeployFixture.cs:34-54` (second breaking run), `src/DbLive/Deployers/Migrations/MigrationVersionDeployer.cs:32-40` (breaking twice)

## Docs to update
- `docs/llms-full.txt` §0 rule 14, §4 (discovery table), §5.3 and §13.3 (second breaking run, breaking twice in the undo mode), §5.6 (guard), §6 ("DbLive never drops code objects", versioned interface), §7, §12 (step 13), §15 (`dblive.code`: column `item_type`, status `removed`, rows of breaking scripts), §17 (new exceptions), §18.6, §18.8
- `docs/deployment/breaking-changes.md` ("Dropping an old procedure version": replace the workaround; "When breaking scripts run": the guard; "In tests"), `docs/deployment/code.md` ("Code is never dropped", "Versioned interface"), `docs/deployment/migrations.md` ("Undo scripts": the test sequence), `docs/deployment/zero-downtime.md` (step 6), `docs/database-as-service.md` ("A procedure is a module with a history"), `docs/project/structure.md` ("Code files"), `docs/unit-testing/index.md` ("Deployment tests")

## Done when
- [ ] Implemented with unit tests: discovery (full and short names, a name that is also a test), order before migration breaking scripts, guard (passes, fails, also for migration breaking scripts), status `removed` (a breaking script of the project is not marked), rows of breaking scripts in `dblive.code`
- [ ] `[SqlDeployFact]` runs a second breaking deployment in the cases with breaking, and each migration breaking script twice in `MigrationBreakingUndoMigration`
- [ ] Integration tests on SQL Server and PostgreSQL: `_v1` → `_v2` with a code breaking script; a rollback to the previous build restores `_v1`; upgrading again and the breaking deployment drop it again; an old version kept in its own deleted file is restored by a rollback; `Breaking` before `Default` fails
- [ ] Docs updated
- [ ] Release note written (0096-19): the guard for migration breaking scripts; code files named like breaking scripts stop deploying as code

## Notes
- PostgreSQL, not part of this task: after a rollback `_v2` stays. If it is a view or a SQL function with `BEGIN ATOMIC` that depends on a column the undo of a migration drops, that `drop column` fails; the undo must drop such code first.
- No warning for an existing code file whose name now makes it a breaking script (`*.breaking.sql`, `*.b.sql`, `breaking.sql`, `b.sql`): DbLive has no users besides the maintainer yet, so the release note is enough.
- Related: `tasks/0096-32-undo-test-schema-compare.md`, `tasks/ideas/undo-test-data-round-trip.md` (came up in the same discussion).
