using Microsoft.Data.SqlClient;

namespace EasyFlow.Tests;

[TestClass]
public class DeploySqlIntegrationTest : IntegrationTestsBase
{
	[TestMethod]
	public void DeployProject_Full()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "Data Source=.;Initial Catalog=EasyFlowTestDBDeploy;Integrated Security=True;";

		RecreateDatabase(sqlConnectionString);

		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(path, sqlConnectionString);
	}

	[TestMethod]
	public void DeployProject_Two_Deployments()
	{
		string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";
		string sqlConnectionString = "Data Source=.;Initial Catalog=EasyFlowTestDBDeploy;Integrated Security=True;";

		RecreateDatabase(sqlConnectionString);

		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlow>();

		// deploy up to version 2
		deploy.DeployProject(path, sqlConnectionString, 2);

		// deploy other.
		deploy.DeployProject(path, sqlConnectionString);
	}

	private static void RecreateDatabase(string sqlConnectionString)
	{
		SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(sqlConnectionString);
		string databaseToDrop = builder.InitialCatalog;
		builder.InitialCatalog = "master";
		SqlConnection cnn = new(builder.ConnectionString);
		cnn.Open();
		var cmd = cnn.CreateCommand();
		cmd.CommandText = @$"
			alter database [{databaseToDrop}] set single_user with rollback immediate;
			drop database [{databaseToDrop}];
			create database [{databaseToDrop}];
		";
		cmd.ExecuteNonQuery();
		cnn.Close();
	}
}