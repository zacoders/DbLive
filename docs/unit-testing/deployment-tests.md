# Deployment Tests

`[SqlDeployFact]` (package `DbLive.xunit`) checks that the whole project deploys to an empty database the way production does, and that the scripts that run on every breaking deployment can run again without changing the schema. It goes into the test project of the [SQL tests](index.md#running-tests-in-visual-studio-and-ci-xunit) and takes the same [fixture builders](index.md#1-a-fixture-builder):

```csharp
using DbLive.MSSQL;
using DbLive.xunit;
using System.Reflection;
using Xunit.Abstractions;

public class DeploymentTests(ITestOutputHelper output)
{
    /// <summary>
    /// Deploys the project to an empty database as production does, in three deployments:
    /// 1. migrations and code, then the SQL tests;
    /// 2. the breaking phase, then the SQL tests again;
    /// 3. the breaking phase once more, which must not change the schema.
    /// Then drops the database.
    /// </summary>
    [SqlDeployFact]
    public Task ReleaseDeploys() => new DeploymentTest(
        fixtureBuilder: new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")),
        dropDatabaseOnComplete: true).DeployAsync(output);
}
```

`DeploymentTest` is not an xUnit fixture, so the test class needs no `IAsyncLifetime`: `DeployAsync` gets a new database from the fixture builder, deploys it, drops it when `dropDatabaseOnComplete` is true, also after a failed deployment, and disposes the builder. When the drop fails after a failed deployment, the test fails with the error of the deployment, and the error of the drop goes to the test output.

## What the test deploys

The test deploys to a fresh database the way production does, in three deployments:

1. `DeployParameters.Default`: migrations and code, then all SQL tests. The release is deployed, its breaking changes are not yet: the old application still runs.
2. `DeployParameters.Breaking`: the breaking phase, then all SQL tests again.
3. `DeployParameters.Breaking` once more. It must not change the schema.

It proves that migrations and breaking scripts apply cleanly, that the breaking deployment is allowed after a normal one, and that the scripts that run on every breaking deployment can run again: the [breaking scripts of the Code folder](../deployment/breaking-changes.md#dropping-an-old-procedure-version), BeforeDeploy and AfterDeploy. The breaking script of a migration runs once, as in production: the test does not prove that a script with `TransactionWrapLevel: None` that failed halfway can run again. The database is new, so an old object version such as `_v1` never existed in it: these tests do not prove that dropping an `_v1` that really exists works.

The [`[SqlFact]` tests](index.md#2-sql-tests-sqlfact) run on a database where migrations, code and breaking scripts were deployed in one run; keep both.

## Schema check

A script that runs without an error can still be wrong: a breaking script whose second run drops more than the first one runs fine. So the deployment test also reads the schema of the database from its catalog before and after the last deployment, `DeployParameters.Breaking`, and the two must be equal.

The comparison covers tables, columns (type, nullability, default, identity, computed expression, collation), keys, foreign keys, check constraints, indexes, views, functions, procedures, triggers, user types and sequences. It ignores DbLive's own `dblive` schema, data, column order, the names SQL Server generates for unnamed constraints, and current identity and sequence values. The full list is in the [agent reference](https://zacoders.github.io/DbLive/llms-full.txt), Section 13.3.

A difference fails the test case with `SchemaChangedException`, which lists what differs. A typical cause is a condition on an object that the script itself drops later (see [In tests](../deployment/breaking-changes.md#in-tests)):

```
Deployment test: a second breaking deployment changed the schema. The breaking scripts of the Code folder and the BeforeDeploy/AfterDeploy scripts run again on it and must not change the schema:
  missing check constraint dbo.Person.CK_Person_Name: check ([Name]<>'')
  missing index dbo.Person.IX_Person_Name: nonclustered (Name asc)
```

`missing` objects were in the earlier schema only, `extra` objects are in the later one only, and `changed` objects show the old and the new definition. Data is not compared. The check needs SQL Server 2017 or later (every SQL Server Docker image) or PostgreSQL 12 or later.
