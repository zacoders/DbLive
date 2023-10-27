namespace EasyFlow.Tests;

public class DeploySqlIntegrationPersistedTest : DeploySqlIntegrationBaseTest
{
	public DeploySqlIntegrationPersistedTest(ITestOutputHelper output) : base(output) { }

	[Fact]
	public void DeployProject_PersistedDbName()
	{
		string dbName = "EasyFlow-PersistedTest";
		DropTestingDatabases(dbName);
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Breaking);
	}
}