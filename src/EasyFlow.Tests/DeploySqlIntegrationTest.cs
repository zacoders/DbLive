namespace EasyFlow.Tests;

public class DeploySqlIntegrationTest : IntegrationTestsBase, IDisposable
{
	readonly string _msSqlTestingProjectPath = Path.GetFullPath(@"TestProject_MSSQL");
	private static string TestDbNamePrefix = "EasyFlow--";

	private static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	public DeploySqlIntegrationTest(ITestOutputHelper output) : base(output)
	{
	}

	public void Dispose()
	{
		DropTestingDatabases();
	}

	[Fact]
	public void DeployProject_Full()
	{
		string dbName = GetRanomDbName();
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, EasyFlowDeployParameters.Default);
	}

	[Fact]
	public void DeployProject_Two_Deployments()
	{
		string dbName = GetRanomDbName();
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		Container.InitializeEasyFlow(DBEngine.MSSQL);

		var deploy = Resolve<IEasyFlow>();

		Log.Information("=== deploy up to version 2 ===");
		EasyFlowDeployParameters parameters = new() { MaxVersionToDeploy = 2 };
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, parameters);

		Log.Information("=== deploy other ===");
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, EasyFlowDeployParameters.Default);
	}

	private static void DropTestingDatabases()
	{
		string sqlConnectionString = $"Data Source=.;Initial Catalog=master;Integrated Security=True;";

		SqlConnection cnn = new(sqlConnectionString);
		cnn.Open();
		var cmd = cnn.CreateCommand();
		cmd.CommandText = $"select name from sys.databases where name like '{TestDbNamePrefix}%'";
		var reader = cmd.ExecuteReader();
		List<string> databases = new();
		while (reader.Read())
		{
			databases.Add(reader.GetString(0));
		}
		reader.Close();
		cnn.Close();

		foreach (var database in databases)
		{
			ServerConnection serverCnn = new(cnn);
			serverCnn.ExecuteNonQuery(new StringCollection {
				$"alter database [{database}] set single_user with rollback immediate;",
				$"drop database[{ database}];"
			});
			serverCnn.Disconnect();
		}
	}
}