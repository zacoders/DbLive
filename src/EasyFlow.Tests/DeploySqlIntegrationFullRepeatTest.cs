namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullRepeatTest : DeploySqlIntegrationBaseTest, IDisposable
{
	string _dbName = GetRanomDbName();

	public DeploySqlIntegrationFullRepeatTest(ITestOutputHelper output) : base(output) { }

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Full_And_Repeat()
	{
		string sqlConnectionString = $"Data Source=.;Initial Catalog={_dbName};Integrated Security=True;TrustServerCertificate=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);

		//repeat, so code should be deployed again
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}
}