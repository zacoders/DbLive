namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullBreakingTest(ITestOutputHelper output) 
	: DeploySqlIntegrationBaseTest(output), IDisposable
{
	string _dbName = GetRanomDbName();

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Full_Plus_Breaking()
	{
		var sqlConnectionString = GetDbConnectionString(_dbName);
		
		var deploy = GetService<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Breaking);
	}
}