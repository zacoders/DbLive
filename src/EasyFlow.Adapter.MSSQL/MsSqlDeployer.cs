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
		//TODO: change isolation level
		string sql = @"
			set xact_abort on;
			set transaction isolation level read committed;
			begin transaction; 
		";
		easyFlowConnection.ServerConnection.ExecuteNonQuery(sql);
	}

	public void CommitTransaction(IEasyFlowSqlConnection cnn)
	{
		var easyFlowConnection = (EasyFlowSqlConnection)cnn;
		easyFlowConnection.ServerConnection.ExecuteNonQuery("commit transaction");
	}

	private IsolationLevel GetMsSqlIsolationLevel(TransactionIsolationLevel isolationLevel) =>
		isolationLevel switch
		{
			TransactionIsolationLevel.Chaos => IsolationLevel.Chaos,
			TransactionIsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
			TransactionIsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
			TransactionIsolationLevel.Serializable => IsolationLevel.Serializable,
			_ => throw new NotSupportedTransactionIsolationLevelException(isolationLevel)
		};

	public void ExecuteNonQuery(IEasyFlowSqlConnection cnn, string sqlStatementt)
	{
		var easyFlowConnection = (EasyFlowSqlConnection)cnn;
		easyFlowConnection.ServerConnection.ExecuteNonQuery(sqlStatementt);
	}
}