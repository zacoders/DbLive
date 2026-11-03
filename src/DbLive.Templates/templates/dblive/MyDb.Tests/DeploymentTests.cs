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
    /// 1. migrations and code, then the SQL tests. Before that, every migration with an undo script goes through
    ///    migration → undo and, if it has a breaking script, migration → breaking → breaking → undo;
    ///    the schema after each undo must equal the schema before the migration;
    /// 2. the breaking phase, then the SQL tests again;
    /// 3. the breaking phase once more, which must not change the schema.
    /// </summary>
    [SqlDeployFact]
    public Task ReleaseDeploysAndRollsBack() => _fixture.DeployAsync(output);
}
