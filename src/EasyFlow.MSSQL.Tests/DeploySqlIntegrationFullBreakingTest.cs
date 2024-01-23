using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;

public class DeploySqlIntegrationFullBreakingTest(
	SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void DeployProject_Full_Plus_Breaking()
	{
		EasyFlow.Deploy(DeployParameters.Default);

		EasyFlow.Deploy(DeployParameters.Breaking);
	}
}