using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using System.Collections.Specialized;

namespace EasyFlow.MSSQL.Tests;

public class SqlServerIntegrationBaseTest : IDisposable
{
	private readonly static string _msSqlTestingProjectPath = Path.GetFullPath(@"DemoMSSQL");
	private static readonly string TestDbNamePrefix = "EasyFlow--";

	protected readonly string masterDbConnectionString;

	protected static string GetRanomDbName() => $"{TestDbNamePrefix}{Guid.NewGuid()}";

	protected readonly string _testingDbName;
	private readonly bool _keepDatabaseAfterTests;
	protected readonly IEasyFlow EasyFlow;

	public ITestOutputHelper Output { get; }

	protected string GetDbConnectionString(string dbName)
	{
		SqlConnectionStringBuilder cnnBuilder = new(masterDbConnectionString)
		{
			InitialCatalog = dbName
		};
		return cnnBuilder.ConnectionString;
	}

	public SqlServerIntegrationBaseTest(ITestOutputHelper output, string? dbName = null, bool keepDatabaseAfterTests = false)
	{
		var config = new TestConfig();

		masterDbConnectionString = config.GetSqlServerConnectionString();
		Output = output;

		if (!string.IsNullOrWhiteSpace(dbName))
		{
			DropTestingDatabase(dbName);
		}

		_testingDbName = dbName ?? GetRanomDbName();
		_keepDatabaseAfterTests = keepDatabaseAfterTests;

		EasyFlow = new EasyFlowBuilder()
			.LogToXUnitOutput(output)
			.AddTestingMsSqlConnection()
			.SqlServer()
			.SetDbConnection(GetDbConnectionString(_testingDbName))
			.SetProjectPath(_msSqlTestingProjectPath)
			.CreateDeployer();
	}

	public void Dispose()
	{
		if (!_keepDatabaseAfterTests)
		{
			DropTestingDatabases(_testingDbName);
		}
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
		bool exists = cmd.ExecuteScalar() != null;
		return exists;
	}
}