using System.Diagnostics.CodeAnalysis;
using Xunit.Extensions.AssemblyFixture;

namespace DbLive.MSSQL.Tests;


[SuppressMessage("Usage", "xUnit1041:Fixture arguments to test classes must have fixture sources", Justification = "AssemblyFixture will be properly supported in xUnit v3. waiting.")]
public class DeploySqlIntegrationTwoTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void DeployProject_Two_Deployments()
	{
		Output.WriteLine("=== deploy up to version 2 ===");
		DeployParameters parameters = new() { MaxVersionToDeploy = 2, DeployCode = false, RunTests = false };
		DbLiveDeployer.Deploy(parameters);

		Output.WriteLine("=== deploy other ===");
		DbLiveDeployer.Deploy(DeployParameters.Default);
	}
}