using Npgsql;

namespace EasyFlow.Adapter.PostgreSQL;

internal class PostreSqlConnection : IEasyFlowSqlConnection
{
	private readonly NpgsqlConnection _connection;

	public PostreSqlConnection(NpgsqlConnection connection)
	{
		_connection = connection;
	}

	public void BeginTransaction(TransactionIsolationLevel isolationLevel)
	{
		var cmd = _connection.CreateCommand();
		cmd.CommandText = "begin transaction";
		cmd.ExecuteNonQuery();
	}

	public void CommitTransaction()
	{
		var cmd = _connection.CreateCommand();
		cmd.CommandText = "commit transaction";
		cmd.ExecuteNonQuery();
	}

	public void ExecuteNonQuery(string sqlStatementt)
	{
		var cmd = _connection.CreateCommand();
		cmd.CommandText = sqlStatementt;
		cmd.ExecuteNonQuery();
	}
}