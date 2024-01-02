namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullTest(ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	[Fact]
	public void DeployProject_Full()
	{
		EasyFlow.Deploy(DeployParameters.Default);
	}
}