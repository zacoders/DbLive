namespace EasyFlow.Tests;

public class DeploySqlIntegrationTest : IntegrationTestsBase
{
	public DeploySqlIntegrationTest(ITestOutputHelper output) : base(output)
	{
	}

	[Fact]
	public void DeployProject_Full()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "Data Source=.;Initial Catalog=EasyFlowTestDBDeploy;Integrated Security=True;";

		RecreateDatabase(sqlConnectionString);

		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(path, sqlConnectionString);
	}

	[Fact]
	public void DeployProject_Two_Deployments()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "Data Source=.;Initial Catalog=EasyFlowTestDBDeploy;Integrated Security=True;";

		RecreateDatabase(sqlConnectionString);

		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlow>();

		Log.Information("=== deploy up to version 2 ===");
		deploy.DeployProject(path, sqlConnectionString, 2);

		Log.Information("=== deploy other ===");
		deploy.DeployProject(path, sqlConnectionString);
	}

	private static void RecreateDatabase(string sqlConnectionString)
	{
		SqlConnectionStringBuilder builder = new(sqlConnectionString);
		string databaseToDrop = builder.InitialCatalog;
		builder.InitialCatalog = "master";
		SqlConnection cnn = new(builder.ConnectionString);
		cnn.Open();
		ServerConnection serverCnn = new(cnn);
		serverCnn.ExecuteNonQuery(new StringCollection {
			$"alter database [{databaseToDrop}] set single_user with rollback immediate;",
			$"drop database[{ databaseToDrop}];",
			$"create database[{ databaseToDrop}];"
		});
		serverCnn.Disconnect();
	}
}