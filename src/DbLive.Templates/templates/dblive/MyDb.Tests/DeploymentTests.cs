#if (mssql)
using DbLive.MSSQL;
#else
using DbLive.PostgreSQL;
#endif
using DbLive.xunit;
using System.Reflection;
using Xunit.Abstractions;

namespace MyDbNamespace.Tests;

public class DeploymentTests(ITestOutputHelper output)
{
    /// <summary>
    /// Deploys MyDb to an empty database as production does, in three deployments:
    /// 1. migrations and code, then the SQL tests;
    /// 2. the breaking phase, then the SQL tests again;
    /// 3. the breaking phase once more, which must not change the schema.
    /// Then drops the database.
    /// </summary>
    [SqlDeployFact]
    public Task ReleaseDeploys() => new DeploymentTest(
#if (mssql)
        new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")),
#else
        new PostgreSqlContainerFixtureBuilder(Assembly.Load("MyDb")),
#endif
        dropDatabaseOnComplete: true).DeployAsync(output);
}
