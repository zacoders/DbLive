using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;

public class DeploySqlIntegrationTwoTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString), IAssemblyFixture<SqlServerIntegrationFixture>
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