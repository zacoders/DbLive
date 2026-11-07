using DbLive.MSSQL;
using DbLive.xunit.Deploy;
using System.Reflection;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;



public class DeploymentTests(ITestOutputHelper _output) : IAsyncLifetime
{
	private readonly DeploymentTestFixture _fixture = new(
		builderProvider: new MsSqlContainerFixtureBuilder(Assembly.Load("DemoMSSQL")),
		dropDatabaseOnComplete: true
	);

	public Task InitializeAsync() => _fixture.InitializeAsync();
	public Task DisposeAsync() => _fixture.DisposeAsync();

	/// <summary>
	/// Deploys the project to an empty database as production does, in three deployments:
	/// 1. migrations, code and the SQL tests;
	/// 2. the breaking phase and the SQL tests again;
	/// 3. the breaking phase once more, which must not change the schema.
	/// </summary>
	[SqlDeployFact]
	public async Task ReleaseDeploys()
	{
		await _fixture.DeployAsync(_output);
	}
}