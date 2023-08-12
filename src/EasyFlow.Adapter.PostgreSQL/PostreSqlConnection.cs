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
		//TODO: support isolation level
		HandleException(() =>
		{
			var cmd = _connection.CreateCommand();
			cmd.CommandText = "begin transaction";
			cmd.ExecuteNonQuery();
		});
	}

	public void CommitTransaction()
	{
		HandleException(() =>
		{
			var cmd = _connection.CreateCommand();
			cmd.CommandText = "commit transaction";
			cmd.ExecuteNonQuery();
		});
	}

	public void ExecuteNonQuery(string sqlStatementt)
	{
		HandleException(() =>
		{
			var cmd = _connection.CreateCommand();
			cmd.CommandText = sqlStatementt;
			cmd.ExecuteNonQuery();
		});
	}

	private static void HandleException(Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			throw new EasyFlowSqlException(e.Message, e);
		}
	}
}