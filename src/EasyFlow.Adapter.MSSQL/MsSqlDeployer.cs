namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDeployer : IEasyFlowDeployer
{
	public IEasyFlowTransaction BeginTransaction(string cnnString, TransactionIsolationLevel isolationLevel)
	{
		using SqlConnection cnn = new(cnnString);
		SqlTransaction sqlTran = cnn.BeginTransaction(GetMsSqlIsolationLevel(isolationLevel));

		return new EasyFlowTransaction(sqlTran);
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

	public void EndTransaction(IEasyFlowTransaction transaction)
	{
		var sqlTran = (EasyFlowTransaction)transaction;
		sqlTran.SqlTransaction.Commit();
	}

	public void ExecuteNonQuery(string cnnString, string sqlStatement, TimeSpan timeout)
	{
		using SqlConnection cnn = new(cnnString);
		Execute(sqlStatement, timeout, cnn);
	}

	public void ExecuteNonQuery(IEasyFlowTransaction transaction, string sqlStatement, TimeSpan timeout)
	{
		var sqlTran = (EasyFlowTransaction)transaction;
		Execute(sqlStatement, timeout, sqlTran.SqlTransaction.Connection);
	}

	private static void Execute(string sqlStatement, TimeSpan timeout, SqlConnection cnn)
	{
		ServerConnection serverCnn = new(cnn);
		serverCnn.StatementTimeout = (int)timeout.TotalSeconds;
		serverCnn.ExecuteNonQuery(sqlStatement);
	}
}