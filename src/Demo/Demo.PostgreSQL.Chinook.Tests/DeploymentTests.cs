using DbLive.Common;
using DbLive.xunit.Deploy;
using Xunit.Abstractions;


namespace Demo.PostgreSQL.Chinook.Tests;


public class DeploymentTests(ITestOutputHelper _output) : IAsyncLifetime
{
	private readonly DeployFixture _fixture = new(
		builderProvider: new DockerPostgresFixtureBuilder(),
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