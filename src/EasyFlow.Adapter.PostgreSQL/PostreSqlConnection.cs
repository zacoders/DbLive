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
		HandleException(() =>
		{
			var cmd = _connection.CreateCommand();
			string isolationStr = GetIsolationLevel(isolationLevel);
            cmd.CommandText = $"begin transaction isolation level {isolationStr}";
			cmd.ExecuteNonQuery();
		});
	}

    private static string GetIsolationLevel(TransactionIsolationLevel isolationLevel) =>
        isolationLevel switch
        {
            TransactionIsolationLevel.Chaos => "read uncommitted",
            TransactionIsolationLevel.ReadCommitted => "read committed",
            TransactionIsolationLevel.RepeatableRead => "repeatable read",
            TransactionIsolationLevel.Serializable => "serializable",
            _ => throw new NotSupportedTransactionIsolationLevelException(isolationLevel)
        };

    public void CommitTransaction()
	{
		HandleException(() =>
		{
			var cmd = _connection.CreateCommand();
			cmd.CommandText = "commit transaction";
			cmd.ExecuteNonQuery();
		});
	}

	public void RollbackTransaction()
	{
		HandleException(() =>
		{
			var cmd = _connection.CreateCommand();
			cmd.CommandText = "rollback transaction";
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

	public void MigrationCompleted(string domain, int migrationVerion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		throw new NotImplementedException();
	}
}