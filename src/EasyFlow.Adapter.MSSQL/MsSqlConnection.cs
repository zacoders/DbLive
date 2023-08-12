namespace EasyFlow.Adapter.MSSQL;

internal class MsSqlConnection : IEasyFlowSqlConnection
{
	private readonly ServerConnection _serverConnection;

	public MsSqlConnection(ServerConnection serverCnn)
	{
		_serverConnection = serverCnn;
	}

	public void BeginTransaction(TransactionIsolationLevel isolationLevel)
	{
		var isolationLevelStr = GetMsSqlIsolationLevel(isolationLevel);
		string sql = @$"
			set xact_abort on;
			set transaction isolation level {isolationLevelStr};
			begin transaction; 
		";
		_serverConnection.ExecuteNonQuery(sql);
	}

	public void CommitTransaction()
	{
		_serverConnection.ExecuteNonQuery("commit transaction");
	}

	public void ExecuteNonQuery(string sqlStatementt)
	{
		_serverConnection.ExecuteNonQuery(sqlStatementt);
	}

	private string GetMsSqlIsolationLevel(TransactionIsolationLevel isolationLevel) =>
		isolationLevel switch
		{
			TransactionIsolationLevel.Chaos => "read uncommitted",
			TransactionIsolationLevel.ReadCommitted => "read committed",
			TransactionIsolationLevel.RepeatableRead => "repeatable read",
			TransactionIsolationLevel.Serializable => "serializable",
			_ => throw new NotSupportedTransactionIsolationLevelException(isolationLevel)
		};
}