namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullRepeatTest(ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	[Fact]
	public void DeployProject_Full_And_Repeat()
	{
		EasyFlow.Deploy(DeployParameters.Default);

		//repeat, so code should be deployed again
		EasyFlow.Deploy(DeployParameters.Default);
	}
}