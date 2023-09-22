namespace EasyFlow.Adapter.PostgreSQL;

internal class PostgreSqlConnection : IEasyFlowSqlConnection
{
	private readonly NpgsqlConnection _connection;

	public PostgreSqlConnection(NpgsqlConnection connection)
	{
		_connection = connection;
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

	public void Close()
	{
		HandleException(_connection.Close);
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

	public void Dispose()
	{
		throw new NotImplementedException();
	}
}