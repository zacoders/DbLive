namespace EasyFlow.Tests;

public class DeploySqlIntegrationTest : IntegrationTestsBase, IDisposable
{
	readonly string _msSqlTestingProjectPath = Path.GetFullPath(@"TestProject_MSSQL");
	private static string TestDbNamePrefix = "EasyFlow--";

	private static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	public DeploySqlIntegrationTest(ITestOutputHelper output) : base(output)
	{
		Container.InitializeEasyFlow(DBEngine.MSSQL);
	}

	public void Dispose()
	{
		DropTestingDatabases();
	}

	[Fact]
	public void DeployProject_PersistedDbName()
	{
		string dbName = "EasyFlow-PersistedTest";
		DropTestingDatabases(new[] { dbName }, true);
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Breaking);
	}

	[Fact]
	public void DeployProject_Full()
	{
		string dbName = GetRanomDbName();
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}

	[Fact]
	public void DeployProject_Full_Plus_Breaking()
	{
		string dbName = GetRanomDbName();
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Breaking);
	}

	[Fact]
	public void DeployProject_Full_And_Repeat()
	{
		string dbName = GetRanomDbName();

		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);

		//repeat, so code should be deployed again
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}

	[Fact]
	public void DeployProject_Two_Deployments()
	{
		string dbName = GetRanomDbName();
		string sqlConnectionString = $"Data Source=.;Initial Catalog={dbName};Integrated Security=True;";

		var deploy = Resolve<IEasyFlow>();

		Log.Information("=== deploy up to version 2 ===");
		DeployParameters parameters = new() { MaxVersionToDeploy = 2, DeployCode = false, RunTests = false };
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, parameters);

		Log.Information("=== deploy other ===");
		deploy.DeployProject(_msSqlTestingProjectPath, sqlConnectionString, DeployParameters.Default);
	}

	private void DropTestingDatabases()
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

		DropTestingDatabases(databases, false);
	}

	private void DropTestingDatabases(IEnumerable<string> databases, bool ifExists)
	{
		string sqlConnectionString = $"Data Source=.;Initial Catalog=master;Integrated Security=True;";

		SqlConnection cnn = new(sqlConnectionString);
		cnn.Open();

		foreach (var database in databases)
		{
			if (ifExists)
			{
				var cmd = cnn.CreateCommand();
				cmd.CommandText = $"select 1 from sys.databases where name = '{database}'";
				bool exists = cmd.ExecuteScalar() == null ? false : true;
				if (!exists) continue;
			}

			ServerConnection serverCnn = new(cnn);
			serverCnn.ExecuteNonQuery(new StringCollection {
				$"alter database [{database}] set single_user with rollback immediate;",
				$"drop database[{ database}];"
			});
			serverCnn.Disconnect();
		}
	}
}