namespace EasyFlow.Tests;

public class DeploySqlIntegrationTwoTest : DeploySqlIntegrationBaseTest, IDisposable
{
	string _dbName = GetRanomDbName();

	public DeploySqlIntegrationTwoTest(ITestOutputHelper output) : base(output) { }

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Two_Deployments()
	{
		string sqlConnectionString = $"Data Source=.;Initial Catalog={_dbName};Integrated Security=True;TrustServerCertificate=True;";

		var deploy = GetService<IEasyFlow>();

		Log.Information("=== deploy up to version 2 ===");
		DeployParameters parameters = new() { MaxVersionToDeploy = 2, DeployCode = false, RunTests = false };
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, parameters);

		Log.Information("=== deploy other ===");
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}
}