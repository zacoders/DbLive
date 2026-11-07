# Breaking Changes

A **breaking change** removes something a running application may still use: a column, a table, a procedure version, a temporary sync trigger. DbLive keeps those changes out of the normal deployment and runs them in a separate **breaking** deployment, after the old application version has been shut down. This is the *contract* half of the expand/contract pattern described in [Database as a Service](../database-as-service.md#the-release-cycle).

There are two kinds of breaking scripts, and both run in the same breaking phase:

- a breaking script of a **migration** changes tables: drops an old column, a table, a temporary sync trigger;
- a breaking script of the **Code folder** drops old versions of code objects, next to the code it cleans up.

## Breaking scripts of migrations

A breaking script of a migration belongs to its version and is named `<version>.breaking.sql` (or `<version>.b.sql`):

```
Migrations/
  20260812090000.split-phones.sql       expand: new table, sync trigger
  20260812090000.breaking.sql           contract: drop trigger and old column
```

```sql
-- 20260812090000.breaking.sql
drop trigger if exists dbo.TMP_DataMigration_Person_To_PersonPhones;
go

alter table dbo.Person drop column if exists PhoneNumber;
go
```

Make breaking scripts tolerant (`if exists`): a breaking script that failed without a transaction runs again on the next deployment.

The breaking phase is the point of no return: once it has dropped the old column, the previous application cannot run any more, and DbLive has no undo scripts. Run it only when the release is confirmed, never for a release whose application was rolled back. See [Roll back a release](migrations.md#roll-back-a-release).

## Dropping an old procedure version

When a procedure's contract changes, its file deploys `_v2` and the old `_v1` stays in the database for the old application (see [Code Deployment](code.md#versioned-interface)). Drop `_v1` in a breaking script of the Code folder, next to the file:

```
Code/
  dbo/
    GetPerson.sql             create or alter proc dbo.GetPerson_v2 (was GetPerson_v1)
    GetPerson.breaking.sql    drop proc if exists dbo.GetPerson_v1
```

```sql
-- Code/dbo/GetPerson.breaking.sql
drop proc if exists dbo.GetPerson_v1;
```

A code-only release needs no migration for it.

- **Names.** `<name>.breaking.sql` next to `<name>.sql`, or `breaking.sql` for its whole folder; short forms `<name>.b.sql` and `b.sql`. Any subfolder of `Code` works. The pair is only a naming convention: DbLive does not link a breaking script to a code file, so a breaking script without a code file is valid (a procedure retired together with its file).
- **Not code, not tests.** These files are never deployed as code and never run as tests. A name that is also a [test file name](../project/structure.md#test-files) (`test.breaking.sql`, `t.b.sql` with the default `TestFilePatterns`) fails the deployment with `BreakingScriptMatchesTestPatternException`, before any of your scripts runs.
- **Every breaking deployment.** There is no "applied once": every code breaking script runs on every breaking deployment, the way AfterDeploy runs on every deployment. **Write them idempotent** (`drop ... if exists`): on a new database `_v1` never existed. A script that fails halfway leaves some old objects dropped and others not (on PostgreSQL a file with several statements is rolled back as a whole); the next breaking deployment finishes the job.
- **Order.** By relative path, before the breaking scripts of migrations, so dependents are dropped before what they depend on: on PostgreSQL a view `_v1` blocks the `drop column` of a migration breaking script.
- **Execution.** Each file is one command without a transaction of its own, like a code file, with the `CodeItemTimeout` timeout. With `TransactionWrapLevel: Deployment` it runs inside the deployment transaction. A failed script stops the deployment with `CodeDeploymentException`.
- **Record.** Each run is written to the file's row in `dblive.code` (`item_type = 'breaking'`, `status` `applied` or `error`, the error text in `error`).
- **Bringing an old version back.** Code is deployed by state: revert `GetPerson.sql` in git and deploy a new release, and the file runs again, because its content differs from what was last deployed, so `_v1` is back; `_v2` stays and does not disturb anything. If the old version lived in a file of its own that a release deleted (`GetPerson_v1.sql`), that file's row was marked `removed` when the release deployed its code, so the file is executed again when a later release brings it back.

Do not drop old versions in `AfterDeploy`: AfterDeploy scripts run on every deployment, including `DeployParameters.Default`, so the drop would run before the old application is gone.

## When breaking scripts run

- Only when `DeployBreaking = true`, for example with `DeployParameters.Breaking`.
- After migrations and code, in the same deployment.
- **Only after the normal deployment of the same build.** Every code file of the project must be deployed with its current content: it has an `applied` row with its current checksum in `dblive.code`. Otherwise the deployment fails with `CodeNotDeployedException`, which says how many code files differ and names a few, and no breaking script runs. It stops a breaking stage that would drop what the running application still uses: started although the `Default` deployment of the build failed, or run before `Default`. It does not check that the release is still the current one: after a rollback of the application the code of the release is still deployed, and after a later release that brought deleted code files back (a `git revert`, or the previous build of a code-only release) every code file of the release can still match. Never start the breaking deployment of a release that was rolled back or replaced. A run that also deploys code (`DeployParameters.Default with { DeployBreaking = true }`) always passes, and so does a project without code files.
- First every breaking script of the Code folder, then the breaking scripts of migrations.
- A breaking script of a migration runs for every version whose **migration is already applied** and whose breaking script is not. It never runs before its migration.
- Breaking scripts of migrations run in version order, **each in its own transaction** with the default `TransactionWrapLevel: Migration`, so the ones that ran before a failed one stay applied. `<version>.breaking.settings.json` sets the [transaction level, isolation level and timeout](../project/configuration.md#item-settings) of one breaking script; the migration's settings file does not apply to it.
- Once applied, a breaking script of a migration is protected by a [checksum](migrations.md#checksums) like a migration. Before it is applied you can still edit it. Breaking scripts of the Code folder have no checksum protection: edit them like code.

```csharp
// The old application is gone: contract the schema.
await deployer.DeployAsync(DeployParameters.Breaking);
```

`DeployParameters.Breaking` deploys no migrations and no code. BeforeDeploy and AfterDeploy scripts run.

## Release flow

The [release cycle](../database-as-service.md#the-release-cycle) in four steps:

1. Deploy with `DeployParameters.Default` (migrations and code).
2. Deploy the new application; old and new instances run side by side.
3. Drain and remove the old application.
4. Deploy with `DeployParameters.Breaking`, once the release is confirmed.

Until step 4 a rollback is a rollback of the application: the database serves both versions. After it, fix forward.

The breaking deployment is safe to repeat: the breaking scripts of the Code folder run again (they are idempotent), and the breaking scripts of migrations that are already applied are skipped (`No migration breaking scripts to apply.`).

## In tests

Test databases usually get everything at once. The `[SqlFact]` fixture deploys migrations, code **and** breaking scripts before the SQL tests run.

The `[SqlDeployFact]` deployment test deploys as production does: `DeployParameters.Default`, then `DeployParameters.Breaking` (with the SQL tests after each of them), then `DeployParameters.Breaking` once more. So the guard above must pass after a normal deployment, and every breaking script of the Code folder runs twice. In the first breaking deployment every breaking script of a migration runs twice in a row, because a breaking script that failed without a transaction runs again on the next deployment. A script whose second run fails or changes the schema fails the test: the test compares the schema after the first and the second run of a migration breaking script, and before and after the second breaking deployment ([schema checks](../unit-testing/deployment-tests.md#schema-checks)). A typical cause is a condition on an object that the script itself drops later:

```sql
-- wrong: the first run drops only the trigger, the second one also drops the column
if object_id('dbo.TMP_sync') is null
    alter table dbo.Person drop column if exists Phone;
drop trigger if exists dbo.TMP_sync;
```

The test database is new, so an old version such as `_v1` never existed there: dropping an `_v1` that really exists is not tested. See [Deployment Tests](../unit-testing/deployment-tests.md).

## What DbLive cannot check

DbLive guarantees the order (breaking runs last, only on request, and only after the normal deployment of the build), not the content. Your team still has to make sure that:

- migrations and code are really backward compatible;
- no old application instance is running when the breaking deployment starts;
- breaking scripts remove every superseded object, column and temporary trigger.

The step-by-step runbook with a checklist is in [Zero-Downtime Deployment](zero-downtime.md).
