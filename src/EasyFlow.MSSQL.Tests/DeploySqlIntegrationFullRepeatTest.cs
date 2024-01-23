using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;

public class DeploySqlIntegrationFullRepeatTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void DeployProject_Full_And_Repeat()
	{
		EasyFlow.Deploy(DeployParameters.Default);

		//repeat, so code should be deployed again
		EasyFlow.Deploy(DeployParameters.Default);
	}
}