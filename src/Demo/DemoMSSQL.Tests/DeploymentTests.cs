using DbLive.Common;
using DbLive.xunit.Deploy;
using Xunit.Abstractions;

namespace DemoMSSQL.Tests;


public class MyDeployFixture()
	: DeployFixture(
		builderProvider: new DockerMsSqlFixtureBuilder(),
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
		await fixture.DeployAsync(_output, deployBreaking, undoTestMode).ConfigureAwait(false);
	}
}
