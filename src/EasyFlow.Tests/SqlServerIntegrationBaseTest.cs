using EasyFlow.Tests.Config;

namespace EasyFlow.Tests;

public class SqlServerIntegrationBaseTest : IntegrationTestsBase
{
	protected readonly static string _msSqlTestingProjectPath = Path.GetFullPath(@"TestProject_MSSQL"); //TODO: hardcoded mssql project?
	private static string TestDbNamePrefix = "EasyFlow--";

	protected static readonly string masterDbConnectionString;

	protected static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	protected static string GetDbConnectionString(string dbName) 
	{
		var cnnBuilder = new SqlConnectionStringBuilder(masterDbConnectionString);
		cnnBuilder.InitialCatalog = dbName;
		return cnnBuilder.ConnectionString;
	}
	static SqlServerIntegrationBaseTest()
	{
		Container.InitializeEasyFlow(DBEngine.MSSQL);
		var config = GetService<TestConfig>();
		masterDbConnectionString = config.GetSqlServerConnectionString();
	}

	public SqlServerIntegrationBaseTest(ITestOutputHelper output) : base(output)
	{		
	}

	private void DropTestingDatabases()
	{
		using SqlConnection cnn = new(masterDbConnectionString);
		cnn.Open();
		var cmd = cnn.CreateCommand();
		cmd.CommandText = $"select name from sys.databases where name like '{TestDbNamePrefix}%'";
		var reader = cmd.ExecuteReader();
		List<string> databases = [];
		while (reader.Read())
		{
			databases.Add(reader.GetString(0));
		}
		reader.Close();
		cnn.Close();

		DropTestingDatabases(databases, false);
	}

	protected void DropTestingDatabases(params string[] databases)
	{
		DropTestingDatabases(databases, true);
	}

	protected void DropTestingDatabase(string database, bool ifExists = true)
	{
		DropTestingDatabases(new[] { database }, ifExists);
	}

	protected void DropTestingDatabases(IEnumerable<string> databases, bool ifExists)
	{
		using SqlConnection cnn = new(masterDbConnectionString);
		cnn.Open();

		foreach (var database in databases)
		{
			if (ifExists)
			{
				bool exists = DbExists(database);
				if (!exists) continue;
			}

			try
			{
				ServerConnection serverCnn = new(cnn);
				serverCnn.ExecuteNonQuery(new StringCollection {
					$"alter database [{database}] set single_user with rollback immediate;",
					$"drop database[{ database}];"
				});
				serverCnn.Disconnect();
			}
			catch (ExecutionFailureException e)
			{
				// if database is not exists, skip error message.
				if (e.Get<SqlException>()?.Number == 5011) return;
			}
		}
	}

	protected bool DbExists(string database)
	{
		using SqlConnection cnn = new(masterDbConnectionString);
		cnn.Open();
		var cmd = cnn.CreateCommand();
		cmd.CommandText = $"select 1 from sys.databases where name = '{database}'";
		bool exists = cmd.ExecuteScalar() == null ? false : true;
		return exists;
	}
}