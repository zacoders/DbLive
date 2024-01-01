namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullMultiRedeployTest(ITestOutputHelper output) 
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	[Fact]
	public void DeployProject_Full_Plus_Breaking()
	{
		EasyFlow.Deploy(DeployParameters.Default);

		EasyFlow.Deploy(DeployParameters.Breaking);

		// Redeploy again

		EasyFlow.Deploy(DeployParameters.Default);

		EasyFlow.Deploy(DeployParameters.Breaking);
	}
}