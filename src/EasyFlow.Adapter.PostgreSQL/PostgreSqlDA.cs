using System.Data.Common;

namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDA : IEasyFlowDA
{
	public void CreateDB(string cnnString, bool skipIfExists = true)
	{
		NpgsqlConnectionStringBuilder cnnBuilder = new(cnnString);

		string? databaseToCreate = cnnBuilder.Database;
		if (databaseToCreate == null) throw new ArgumentException("Database must be specified in connection string.");

		cnnBuilder.Database = "postgres";

		var cnn = new NpgsqlConnection(cnnBuilder.ConnectionString);
		cnn.Open();

		var cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from pg_catalog.pg_database where lower(datname) = lower(:name)";
		cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)cmd.ExecuteScalar() == 1;

		if (dbExists && skipIfExists) return;

		cnn.Execute($"""create database "{databaseToCreate}" """);
		cnn.Close();
	}

	public bool EasyFlowInstalled(string cnnString)
	{
		throw new NotImplementedException();
	}

	public void ExecuteNonQuery(string cnnString, string sqlStatementt)
	{
		try
		{
			using var cnn = new NpgsqlConnection(cnnString);
			cnn.Open();
			var cmd = cnn.CreateCommand();
			cmd.CommandText = sqlStatementt;
			cmd.ExecuteNonQuery();
		}
		catch (Exception e)
		{
			throw new EasyFlowSqlException(e.Message, e);
		}		
	}

	public int GetEasyFlowVersion(string cnnString)
	{
		throw new NotImplementedException();
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		throw new NotImplementedException();
	}

	public void MigrationCompleted(string cnnString, int migrationVerion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc)
	{
		throw new NotImplementedException();
	}

	public void SetEasyFlowVersion(string cnnString, int version, DateTime migrationCompletedUtc)
	{
		throw new NotImplementedException();
	}
}
