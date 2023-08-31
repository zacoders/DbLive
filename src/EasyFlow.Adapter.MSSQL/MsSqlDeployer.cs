namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDeployer : IEasyFlowDeployer
{
	public void CreateDB(string cnnString, bool skipIfExists = true)
	{
		SqlConnectionStringBuilder builder = new(cnnString);
		string databaseToCreate = builder.InitialCatalog;
		builder.InitialCatalog = "master";

		SqlConnection cnn = new(builder.ConnectionString);
		cnn.Open();

		var cmd = cnn.CreateCommand();
		cmd.CommandText = "select 1 from sys.databases where name = @name";
		cmd.Parameters.AddWithValue("name", databaseToCreate);
		bool dbExists = (int?)cmd.ExecuteScalar() == 1;

		if (dbExists && skipIfExists) return;

		ServerConnection serverCnn = new(cnn);
		serverCnn.ExecuteNonQuery($"create database [{databaseToCreate}];");

		serverCnn.Disconnect();
	}

	public IEasyFlowSqlConnection OpenConnection(string cnnString)
	{
		SqlConnection cnn = new(cnnString);
		cnn.Open();

		ServerConnection serverCnn = new(cnn)
		{
			StatementTimeout = (int)TimeSpan.FromDays(30).TotalSeconds
		};

		return new MsSqlConnection(serverCnn);
	}
}