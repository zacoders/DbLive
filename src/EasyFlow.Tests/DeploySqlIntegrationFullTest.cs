namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullTest : DeploySqlIntegrationBaseTest, IDisposable
{
	string _dbName = GetRanomDbName();

	public DeploySqlIntegrationFullTest(ITestOutputHelper output) : base(output) { }

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Full()
	{
		string sqlConnectionString = $"Data Source=.;Initial Catalog={_dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}
}