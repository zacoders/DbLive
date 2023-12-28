namespace EasyFlow.Tests;

public class DeploySqlIntegrationTwoTest(ITestOutputHelper output) 
	: SqlServerIntegrationBaseTest(output), IDisposable
{
	string _dbName = GetRanomDbName();

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Two_Deployments()
	{
		var sqlConnectionString = GetDbConnectionString(_dbName);

		var deploy = GetService<IEasyFlow>();

		Log.Information("=== deploy up to version 2 ===");
		DeployParameters parameters = new() { MaxVersionToDeploy = 2, DeployCode = false, RunTests = false };
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, parameters);

		Log.Information("=== deploy other ===");
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}
}