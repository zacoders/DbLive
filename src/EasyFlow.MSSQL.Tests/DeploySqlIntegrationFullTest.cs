using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;

public class DeploySqlIntegrationFullTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void DeployProject_Full()
	{
		EasyFlow.Deploy(DeployParameters.Default);
	}
}