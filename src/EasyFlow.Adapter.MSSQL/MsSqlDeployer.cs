namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDeployer : IEasyFlowDeployer
{
	public IEasyFlowSqlConnection OpenConnection(string cnnString)
	{
		SqlConnection cnn = new(cnnString);
		cnn.Open();

		ServerConnection serverCnn = new(cnn);
		serverCnn.StatementTimeout = (int)TimeSpan.FromDays(30).TotalSeconds;

		return new EasyFlowSqlConnection(serverCnn);
	}

	public void BeginTransaction(IEasyFlowSqlConnection cnn, TransactionIsolationLevel isolationLevel)
	{
		var easyFlowConnection = (EasyFlowSqlConnection)cnn;
		var isolationLevelStr = GetMsSqlIsolationLevel(isolationLevel);
		string sql = @$"
			set xact_abort on;
			set transaction isolation level {isolationLevelStr};
			begin transaction; 
		";
		easyFlowConnection.ServerConnection.ExecuteNonQuery(sql);
	}

	public void CommitTransaction(IEasyFlowSqlConnection cnn)
	{
		var easyFlowConnection = (EasyFlowSqlConnection)cnn;
		easyFlowConnection.ServerConnection.ExecuteNonQuery("commit transaction");
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

	public void ExecuteNonQuery(IEasyFlowSqlConnection cnn, string sqlStatementt)
	{
		var easyFlowConnection = (EasyFlowSqlConnection)cnn;
		easyFlowConnection.ServerConnection.ExecuteNonQuery(sqlStatementt);
	}
}