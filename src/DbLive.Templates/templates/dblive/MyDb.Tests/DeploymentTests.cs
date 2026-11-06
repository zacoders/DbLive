#if (mssql)
using DbLive.MSSQL;
#else
using DbLive.PostgreSQL;
#endif
using DbLive.xunit.Deploy;
using System.Reflection;
using Xunit.Abstractions;

namespace MyDbNamespace.Tests;

public class DeploymentTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly DeploymentTestFixture _fixture = new(
#if (mssql)
        new MsSqlContainerFixtureBuilder(Assembly.Load("MyDb")),
#else
        new PostgreSqlContainerFixtureBuilder(Assembly.Load("MyDb")),
#endif
        dropDatabaseOnComplete: true);

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public Task DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>
    /// Deploys MyDb to an empty database as production does, in three deployments:
    /// 1. migrations and code, then the SQL tests;
    /// 2. the breaking phase, where the breaking script of every migration runs twice and its second run
    ///    must not change the schema, then the SQL tests again;
    /// 3. the breaking phase once more, which must not change the schema.
    /// </summary>
    [SqlDeployFact]
    public Task ReleaseDeploys() => _fixture.DeployAsync(output);
}
