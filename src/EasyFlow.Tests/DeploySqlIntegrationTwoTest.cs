namespace EasyFlow.Tests;

public class DeploySqlIntegrationTwoTest(ITestOutputHelper output) 
	: SqlServerIntegrationBaseTest(output), IDisposable
{	
	[Fact]
	public void DeployProject_Two_Deployments()
	{
		Output.WriteLine("=== deploy up to version 2 ===");
		DeployParameters parameters = new() { MaxVersionToDeploy = 2, DeployCode = false, RunTests = false };
		EasyFlow.Deploy(parameters);

		Output.WriteLine("=== deploy other ===");
		EasyFlow.Deploy(DeployParameters.Default);
	}
}