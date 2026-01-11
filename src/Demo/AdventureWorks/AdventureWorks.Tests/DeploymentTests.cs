using DbLive.Common;
using DbLive.xunit.Deploy;
using Xunit;
using Xunit.Abstractions;

namespace AdventureWorks.Tests;


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
	[SqlDeployTest]
	public async Task Deploy(bool deployBreaking, UndoTestMode undoTestMode)
	{
		await fixture.DeployAsync(_output, deployBreaking, undoTestMode).ConfigureAwait(false);
	}
}
