using Xunit.Extensions.AssemblyFixture;

namespace EasyFlow.MSSQL.Tests;

public class DeploySqlIntegrationPersistedTest(SqlServerIntegrationFixture _fixture, ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, _fixture.MasterDbConnectionString, dbName: "EasyFlow-PersistedTest"), IAssemblyFixture<SqlServerIntegrationFixture>
{
	[Fact]
	public void DeployProject_PersistedDbName()
	{
		EasyFlow.Deploy(DeployParameters.Default);
		EasyFlow.Deploy(DeployParameters.Breaking);
	}
}