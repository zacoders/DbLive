# Deployment Tests

`[SqlDeployFact]` (package `DbLive.xunit`) checks that the whole project deploys to an empty database the way production does, and that breaking scripts can run again without changing the schema. It goes into the test project of the [SQL tests](index.md#running-tests-in-visual-studio-and-ci-xunit) and takes the same [fixture builders](index.md#1-a-fixture-builder):

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
    /// 1. migrations and code, then the SQL tests;
    /// 2. the breaking phase, where the breaking script of every migration runs twice and its second run
    ///    must not change the schema, then the SQL tests again;
    /// 3. the breaking phase once more, which must not change the schema.
    /// </summary>
    [SqlDeployFact]
    public async Task ReleaseDeploys()
        => await _fixture.DeployAsync(output);
}
```

## What the test deploys

The test deploys to a fresh database the way production does, in three deployments:

1. `DeployParameters.Default`: migrations and code, then all SQL tests. The release is deployed, its breaking changes are not yet: the old application still runs.
2. `DeployParameters.Breaking`: the breaking phase, then all SQL tests again. The breaking script of every migration runs twice in a row.
3. `DeployParameters.Breaking` once more. It must not change the schema.

It proves that migrations apply cleanly, that the breaking deployment is allowed after a normal one, and that every breaking script can run again: a breaking script of a migration that failed with `TransactionWrapLevel: None` runs again on the next deployment, over what its first run already changed; the [breaking scripts of the Code folder](../deployment/breaking-changes.md#dropping-an-old-procedure-version) run on every breaking deployment; BeforeDeploy and AfterDeploy scripts run on every deployment. The database is new, so an old object version such as `_v1` never existed in it: these tests do not prove that dropping an `_v1` that really exists works.

The [`[SqlFact]` tests](index.md#2-sql-tests-sqlfact) run on a database where migrations, code and breaking scripts were deployed in one run; keep both.

## Schema checks

A script that runs without an error can still be wrong: a breaking script whose second run drops more than the first one runs fine. So the deployment test also reads the schema of the database from its catalog and compares it at two points:

| Check | Must be equal |
|-------|---------------|
| Second breaking run (first breaking deployment) | the schema after the first and after the second run of a migration breaking script |
| Second breaking deployment | the schema before and after the last deployment, `DeployParameters.Breaking` |

The comparison covers tables, columns (type, nullability, default, identity, computed expression, collation), keys, foreign keys, check constraints, indexes, views, functions, procedures, triggers, user types and sequences. It ignores DbLive's own `dblive` schema, data, column order, the names SQL Server generates for unnamed constraints, and current identity and sequence values. The full list is in the [agent reference](https://zacoders.github.io/DbLive/llms-full.txt), Section 13.3.

A difference fails the test case with `SchemaChangedException`, which names the check and the script and lists what differs. A typical cause is a condition on an object that the script itself drops later (see [In tests](../deployment/breaking-changes.md#in-tests)):

```
Deployment test: the breaking script Migrations/002.breaking.sql is not idempotent, its second run changed the schema:
  missing check constraint dbo.Person.CK_Person_Name: check ([Name]<>'')
  missing index dbo.Person.IX_Person_Name: nonclustered (Name asc)
```

`missing` objects were in the earlier schema only, `extra` objects are in the later one only, and `changed` objects show the old and the new definition. Data is not compared. The checks need SQL Server 2017 or later (every SQL Server Docker image) or PostgreSQL 12 or later.
