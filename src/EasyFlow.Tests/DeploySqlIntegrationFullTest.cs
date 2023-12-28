namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullTest(ITestOutputHelper output) 
	: DeploySqlIntegrationBaseTest(output), IDisposable
{
	string _dbName = GetRanomDbName();

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Full()
	{
		var sqlConnectionString = GetDbConnectionString(_dbName);

		var deploy = GetService<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}
}