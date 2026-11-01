using DbLive.xunit.Deploy;
using Xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;



public class DeploymentTests(ITestOutputHelper _output) : IAsyncLifetime
{
	private readonly DeploymentTestFixture _fixture = new(
		builderProvider: new DockerMsSqlFixtureBuilder(),
		dropDatabaseOnComplete: true
	);

	public Task InitializeAsync() => _fixture.InitializeAsync();
	public Task DisposeAsync() => _fixture.DisposeAsync();

	/// <summary>
	/// Deploys the project to an empty database as production does, in three deployments:
	/// 1. migrations, code and the SQL tests. Before its final run, every migration with an undo script goes through
	///    migration → undo and, if it has a breaking script, migration → breaking → breaking → undo;
	///    the schema after each undo must equal the schema before the migration;
	/// 2. the breaking phase and the SQL tests again;
	/// 3. the breaking phase once more, which must not change the schema.
	/// </summary>
	[SqlDeployFact]
	public async Task ReleaseDeploysAndRollsBack()
	{
		await _fixture.DeployAsync(_output);
	}
}