# Deployment Tests

`[SqlDeployFact]` (package `DbLive.xunit`) checks that the whole project deploys to an empty database the way production does, and that undo scripts restore the schema. It goes into the test project of the [SQL tests](index.md#running-tests-in-visual-studio-and-ci-xunit) and takes the same [fixture builders](index.md#1-a-fixture-builder):

```csharp
using DbLive.MSSQL;
using DbLive.xunit.Deploy;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

public class DeploymentTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly DeploymentTestFixture _fixture = new(
        builderProvider: new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")),
        dropDatabaseOnComplete: true);

    public Task InitializeAsync() => _fixture.InitializeAsync();
    public Task DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>
    /// Deploys the project to an empty database as production does, in three deployments:
    /// 1. migrations and code, then the SQL tests. Before its final run, every migration with an undo script goes through
    ///    migration → undo and, if it has a breaking script, migration → breaking → breaking → undo;
    ///    the schema after each undo must equal the schema before the migration;
    /// 2. the breaking phase, then the SQL tests again;
    /// 3. the breaking phase once more, which must not change the schema.
    /// </summary>
    [SqlDeployFact]
    public async Task ReleaseDeploysAndRollsBack()
        => await _fixture.DeployAsync(output);
}
```

## What the test deploys

The test deploys to a fresh database the way production does, in three deployments:

1. `DeployParameters.Default`: migrations and code, then all SQL tests. The release is deployed, its breaking changes are not yet: the old application still runs.
2. `DeployParameters.Breaking`: the breaking phase, then all SQL tests again.
3. `DeployParameters.Breaking` once more. It must not change the schema.

In the first deployment, every migration version that has an undo script goes through the undo cycle before it is applied for good:

| Step | Sequence |
|------|----------|
| Rollback before the breaking phase | migration, undo |
| Rollback after the breaking phase (versions with a breaking script) | migration, breaking, breaking, undo |
| Final | migration |

It proves that migrations apply cleanly, that undo scripts revert them in both states a rollback can find the database in, and that a version can be re-applied after an undo. Versions without an undo script are deployed once.

Data is not compared, so the SQL tests of the deployment test run on data the undo cycles went through. The [`[SqlFact]` tests](index.md#2-sql-tests-sqlfact) run on a database where every migration ran once; keep both. They also show whether the release deploys when a failing undo stops the deployment test early.

The breaking deployments run on their own, as in production. They prove that the breaking deployment is allowed after a normal one, that the [breaking scripts of the Code folder](../deployment/breaking-changes.md#dropping-an-old-procedure-version) can run again (they run on every breaking deployment), and that BeforeDeploy and AfterDeploy scripts can run again too. The breaking script of a migration runs twice in the undo cycle, because a failed breaking script with `TransactionWrapLevel: None` runs again on the next deployment. The database is new, so an old object version such as `_v1` never existed in it: these tests do not prove that dropping an `_v1` that really exists works.

## Schema checks

A script that runs without an error can still be wrong: an undo that forgets to add back the column the breaking script dropped runs fine, and so does the migration after it. So the deployment test also reads the schema of the database from its catalog and compares it at three points:

| Check | Must be equal |
|-------|---------------|
| Undo (after each undo of the cycle) | the schema before the migration and the schema after its undo |
| Second breaking run (undo cycle) | the schema after the first and after the second run of a migration breaking script |
| Second breaking deployment | the schema before and after the last deployment, `DeployParameters.Breaking` |

The second check matters for rollbacks: in production a breaking script runs once and the rollback runs the undo after that one run. If the second run of the script changes more than the first one, the test would check the undo against a state production never has, so it fails instead.

The comparison covers tables, columns (type, nullability, default, identity, computed expression, collation), keys, foreign keys, check constraints, indexes, views, functions, procedures, triggers, user types and sequences. It ignores DbLive's own `dblive` schema, data, column order (an undo can only add a column back at the end of the table), the names SQL Server generates for unnamed constraints, and current identity and sequence values. The full list is in the [agent reference](https://zacoders.github.io/DbLive/llms-full.txt), Section 13.3.

A difference fails the test case with `SchemaChangedException`, which names the check and the script and lists what differs:

```
Undo test: Migrations/002.undo.sql does not restore the schema. The schema after the undo differs from the schema before Migrations/002.migration.sql:
  missing check constraint dbo.Person.CK_Person_Name: check ([Name]<>'')
  missing index dbo.Person.IX_Person_Name: nonclustered (Name asc)
```

An undo that fails only after the breaking script says so; usually the breaking script dropped a column and the undo does not add it back:

```
Undo test: Migrations/002.undo.sql does not restore the schema after the breaking script Migrations/002.breaking.sql. The schema after the undo differs from the schema before Migrations/002.migration.sql:
  missing column dbo.Person.Phone: varchar(20) collate SQL_Latin1_General_CP1_CI_AS null
```

`missing` objects were in the earlier schema only, `extra` objects are in the later one only, and `changed` objects show the old and the new definition. Data is not compared. The checks need SQL Server 2017 or later (every SQL Server Docker image) or PostgreSQL 12 or later.
