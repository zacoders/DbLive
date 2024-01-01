namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullBreakingTest(ITestOutputHelper output) 
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	[Fact]
	public void DeployProject_Full_Plus_Breaking()
	{
		EasyFlow.Deploy(DeployParameters.Default);

		EasyFlow.Deploy(DeployParameters.Breaking);
	}
}