namespace EasyFlow.MSSQL.Tests;

public class DeploySqlIntegrationPersistedTest(ITestOutputHelper output)
	: SqlServerIntegrationBaseTest(output, dbName: "EasyFlow-PersistedTest", keepDatabaseAfterTests: true)
{
	[Fact]
	public void DeployProject_PersistedDbName()
	{
		EasyFlow.Deploy(DeployParameters.Default);
		EasyFlow.Deploy(DeployParameters.Breaking);
	}
}