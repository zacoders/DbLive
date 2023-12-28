namespace EasyFlow.Tests;

public class DeploySqlIntegrationPersistedTest(ITestOutputHelper output) : DeploySqlIntegrationBaseTest(output)
{
	string _dbName = "EasyFlow-PersistedTest";

	[Fact]
	public void DeployProject_PersistedDbName()
	{
		var sqlConnectionString = GetDbConnectionString(_dbName);

		DropTestingDatabases(_dbName);
		
		var deploy = GetService<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Breaking);
	}
}