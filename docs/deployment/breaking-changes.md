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
- **Execution.** Each file runs without a transaction of its own (unlike a code file), with the `CodeItemTimeout` timeout for the whole file. With `TransactionWrapLevel: Deployment` it runs inside the deployment transaction. A failed script stops the deployment with `CodeDeploymentException`: its message names the file and holds the error of the server.
- **Record.** Each successful run is written to the file's row in `dblive.code` (`item_type = 'breaking'`, `status` `applied`). A failed run writes nothing: the error is in the exception and the log.
- **Bringing an old version back.** Code is deployed by state: revert `GetPerson.sql` in git and deploy a new release, and the file runs again, because its content differs from what was last deployed, so `_v1` is back; `_v2` stays and does not disturb anything. Not with [one script for all old versions](#one-script-for-all-old-versions): there, give the old contract the next number. If the old version lived in a file of its own that a release deleted (`GetPerson_v1.sql`), that file's row was marked `removed` when the release deployed its code, so the file is executed again when a later release brings it back.

Do not drop old versions in `AfterDeploy`: AfterDeploy scripts run on every deployment, including `DeployParameters.Default`, so the drop would run before the old application is gone.

### One script for all old versions

Instead of a breaking script next to each file, a project can drop the old versions by name in one breaking script of the `Code` folder: every object named `<name>_v<N>` for which an object of the same kind, schema and name with a greater `N` exists. The breaking phase starts only when no application calls the old versions, so the script drops what the scripts next to the files would drop, and nobody has to write a drop for each change.

Use it only when all of these hold:

- Every interface object has `_v<N>` in its name from the first version on (`GetPerson_v1`, not `GetPerson`). An object without it is never dropped.
- **The version number only grows.** To bring an old contract back, give it the next number (`GetPerson_v3` with the body of `_v1`); do not revert the file to the lower name. DbLive never drops code, so `_v2` stays in the database, and the script drops `_v1`, which the application calls again. The tests do not find it: their new database never had `_v2`.
- No database gets a version that the repository does not have, such as a shared development database where branches are deployed.

=== "SQL Server"

    ```sql
    -- Code/drop-old-versions.breaking.sql
    -- Drops every version but the latest of each procedure, function, view and user-defined type
    -- named <name>_v<N> in the listed schemas: dbo.GetUser_v1 and dbo.GetUser_v2 when dbo.GetUser_v3 exists.
    -- The version number only grows: never give a file a lower version name again.
    declare @drops table (id int identity primary key, stmt nvarchar(max) not null);

    with objects as (
        select schema_name(schema_id) as schema_name, name,
               case type when 'V' then 1 when 'P' then 2 else 3 end as kind
        from sys.objects
        where type in ('V', 'P', 'FN', 'IF', 'TF') and is_ms_shipped = 0
        union all
        select schema_name(schema_id), name, 4
        from sys.types
        where is_user_defined = 1
    ), versioned as (
        select o.schema_name, o.name, o.kind,
               left(o.name, len(o.name) - x.p - 1) as base_name,
               try_cast(d.digits as int) as version
        from objects o
        cross apply (select charindex('v_', reverse(o.name)) as p) x
        cross apply (select case when x.p > 1 then right(o.name, x.p - 1) end as digits) d
        where o.schema_name in (N'dbo') -- the schemas of the project
          and d.digits not like '%[^0-9]%'
    ), latest as (
        select *, max(version) over (partition by schema_name, kind, base_name) as latest_version
        from versioned
        where version is not null
    )
    insert @drops (stmt)
    select N'drop ' + choose(kind, N'view', N'proc', N'function', N'type') + N' '
           + quotename(schema_name) + N'.' + quotename(name) + N';'
    from latest
    where version < latest_version
    order by kind, schema_name, name;

    declare @id int = 0, @stmt nvarchar(max);
    while 1 = 1
    begin
        select top (1) @id = id, @stmt = stmt from @drops where id > @id order by id;
        if @@rowcount = 0 break;
        exec sp_executesql @stmt;
    end;
    ```

=== "PostgreSQL"

    ```sql
    -- Code/drop-old-versions.breaking.sql
    -- Drops every version but the latest of each function, procedure and view
    -- named <name>_v<N> in the listed schemas: get_user_v1 and get_user_v2 when get_user_v3 exists.
    -- The version number only grows: never give a file a lower version name again.
    do $$
    declare
      old_views text;
      old_routines text;
    begin
      with objects as (
        select n.nspname as schema_name, p.proname as name, p.prokind::text as kind,
               format('%I.%I(%s)', n.nspname, p.proname, pg_get_function_identity_arguments(p.oid)) as target
        from pg_proc p
        join pg_namespace n on n.oid = p.pronamespace
        where p.prokind in ('f', 'p')
          and not exists (select from pg_depend d where d.classid = 'pg_proc'::regclass and d.objid = p.oid and d.deptype = 'e')
        union all
        select n.nspname, c.relname, 'v', format('%I.%I', n.nspname, c.relname)
        from pg_class c
        join pg_namespace n on n.oid = c.relnamespace
        where c.relkind = 'v'
          and not exists (select from pg_depend d where d.classid = 'pg_class'::regclass and d.objid = c.oid and d.deptype = 'e')
      ), versioned as (
        select *,
               substring(name from '^(.*)_v[0-9]{1,9}$') as base_name,
               substring(name from '_v([0-9]{1,9})$')::int as version
        from objects
        where schema_name in ('public') -- the schemas of the project
      ), latest as (
        select *, max(version) over (partition by schema_name, kind, base_name) as latest_version
        from versioned
        where version is not null
      )
      select string_agg(target, ', ') filter (where kind = 'v'),
             string_agg(target, ', ') filter (where kind <> 'v')
      into old_views, old_routines
      from latest
      where version < latest_version;

      -- views first: a view that calls an old function blocks its drop
      if old_views is not null then
        execute 'drop view ' || old_views;
      end if;
      if old_routines is not null then
        execute 'drop routine ' || old_routines;
      end if;
    end $$;
    ```

Set the schemas of your project in the script. What else to know:

- **Kinds.** Procedures, functions and views; on SQL Server also user-defined types (table types). A version is compared within its kind only: a procedure `GetPerson_v1` stays when `GetPerson_v2` is a function. Drop such an object, a kind the script does not cover, and an object retired together with its file in a breaking script next to the file, as above.
- **What stays.** Objects in other schemas, which DBAs may create outside DbLive, and on PostgreSQL the functions of extensions (`uuid-ossp` installs `uuid_generate_v1` to `uuid_generate_v5` in `public`).
- **Numbers.** `_v10` is later than `_v9`.
- **Dependencies.** On SQL Server the script drops views first, then procedures, functions and types, so a view `with schemabinding` goes before its function and a procedure before its table type. On PostgreSQL all old views go in one statement and all old functions and procedures in another, so old objects that depend on each other do not block it. An object without a version name that uses an old version (a view that reads `users_v1`) blocks the drop on PostgreSQL: drop it in a breaking script that sorts before this one. On SQL Server nothing blocks the drop without `schemabinding`, and such an object fails when it is called; the SQL tests find it, because their new database has only the current versions.
- **Every breaking deployment.** The script reads the catalog, so a second run and a new database find nothing to drop.

## When breaking scripts run

- Only when `DeployBreaking = true`, for example with `DeployParameters.Breaking`.
- After migrations and code, in the same deployment.
- **Only when every code file of the build is deployed.** Every code file of the project must be deployed with its current content: it has an `applied` row with its current checksum in `dblive.code`. Otherwise the deployment fails with `CodeNotDeployedException`, which says how many code files differ and names a few, and no breaking script runs. It stops a breaking stage that would drop what the running application still uses, started although the `Default` deployment of the build failed in its code phase. Only code files are checked: a `Default` deployment that failed earlier (for example in a migration, after the migrations before it were committed) or did not run is caught only when the build added or changed a code file. Otherwise the breaking scripts of the Code folder and of the applied migrations run, so start the breaking deployment only after the `Default` deployment of the same build succeeded. It does not check that the release is still the current one: after a rollback of the application the code of the release is still deployed, and after a later release that brought deleted code files back (a `git revert`, or the previous build of a code-only release) every code file of the release can still match. Never start the breaking deployment of a release that was rolled back or replaced. A run that also deploys code (`DeployParameters.Default with { DeployBreaking = true }`) always passes, and so does a project without code files.
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

The `[SqlDeployFact]` deployment test deploys as production does: `DeployParameters.Default`, then `DeployParameters.Breaking` (with the SQL tests after each of them), then `DeployParameters.Breaking` once more. So the guard above must pass after a normal deployment, and every breaking script of the Code folder runs twice; the breaking script of a migration runs once. A code breaking script whose second run fails or changes the schema fails the test: the test compares the schema before and after the second breaking deployment ([schema check](../unit-testing/deployment-tests.md#schema-check)). A typical cause is a condition on an object that the script itself drops later:

```sql
-- wrong: the first run drops only GetUser_v1, the second one also drops the column
if object_id('dbo.GetUser_v1') is null
    alter table dbo.Person drop column if exists Phone;
drop proc if exists dbo.GetUser_v1;
```

The test database is new, so an old version such as `_v1` never existed there: dropping an `_v1` that really exists is not tested. See [Deployment Tests](../unit-testing/deployment-tests.md).

## What DbLive cannot check

DbLive guarantees the order (breaking runs last, only on request, and only when every code file of the build is deployed), not the content. Your team still has to make sure that:

- migrations and code are really backward compatible;
- no old application instance is running when the breaking deployment starts;
- the breaking deployment starts only after the `Default` deployment of the same build succeeded, and never for a release that was rolled back or replaced;
- breaking scripts remove every superseded object, column and temporary trigger.

The step-by-step runbook with a checklist is in [Zero-Downtime Deployment](zero-downtime.md).
