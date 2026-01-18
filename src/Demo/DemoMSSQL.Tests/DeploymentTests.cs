using DbLive.Common;
using DbLive.xunit.Deploy;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;



public class DeploymentTests(ITestOutputHelper _output) : IAsyncLifetime
{
	private readonly DeployFixture _fixture = new(
		builderProvider: new DockerMsSqlFixtureBuilder(),
		dropDatabaseOnComplete: true
	);

	public Task InitializeAsync() => _fixture.InitializeAsync();
	public Task DisposeAsync() => _fixture.DisposeAsync();

	[SqlDeployFact]
	public async Task Deploy(bool deployBreaking, UndoTestMode undoTestMode)
	{
		await _fixture.DeployAsync(_output, deployBreaking, undoTestMode);
	}
}