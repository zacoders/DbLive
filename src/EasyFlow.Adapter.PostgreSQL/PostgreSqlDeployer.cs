using Dapper;

namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDeployer : IEasyFlowDeployer
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

	public IEasyFlowSqlConnection OpenConnection(string connectionString)
	{
		var cnn = new NpgsqlConnection(connectionString);
		cnn.Open();
		return new PostreSqlConnection(cnn);
	}
}