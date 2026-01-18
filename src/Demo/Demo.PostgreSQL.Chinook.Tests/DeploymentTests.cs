using DbLive.Common;
using DbLive.xunit.Deploy;
using Xunit.Abstractions;


namespace Demo.PostgreSQL.Chinook.Tests;


public class MyDeployFixture()
	: DeployFixture(
		builderProvider: new DockerPostgresFixtureBuilder(),
		dropDatabaseOnComplete: true
	  )
{
}

public class DeploymentTests(ITestOutputHelper _output, MyDeployFixture fixture)
	: IClassFixture<MyDeployFixture>
{
	[SqlDeployFact]
	public async Task Deploy(bool deployBreaking, UndoTestMode undoTestMode)
	{
		await fixture.DeployAsync(_output, deployBreaking, undoTestMode);
	}
}
