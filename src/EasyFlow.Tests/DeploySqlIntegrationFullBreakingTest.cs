namespace EasyFlow.Tests;

public class DeploySqlIntegrationFullBreakingTest : DeploySqlIntegrationBaseTest, IDisposable
{
	string _dbName = GetRanomDbName();

	public DeploySqlIntegrationFullBreakingTest(ITestOutputHelper output) : base(output) { }

	public void Dispose()
	{
		DropTestingDatabases(_dbName);
	}

	[Fact]
	public void DeployProject_Full_Plus_Breaking()
	{
		string sqlConnectionString = $"Data Source=.;Initial Catalog={_dbName};Integrated Security=True;TrustServerCertificate=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Breaking);
	}
}