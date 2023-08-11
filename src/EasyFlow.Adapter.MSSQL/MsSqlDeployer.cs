namespace EasyFlow.Adapter.MSSQL;

public class MsSqlDeployer : IEasyFlowDeployer
{
	public IEasyFlowTransaction BeginTransaction(string cnnString, TransactionIsolationLevel isolationLevel)
	{
		SqlConnection cnn = new(cnnString);
		cnn.Open();
		SqlTransaction sqlTran = cnn.BeginTransaction(GetMsSqlIsolationLevel(isolationLevel));

		return new MsSqlTransaction(sqlTran);
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

	public void ExecuteNonQuery(string cnnString, string sqlStatement, TimeSpan timeout)
	{
		using SqlConnection cnn = new(cnnString);
		cnn.Open();
		Execute(sqlStatement, timeout, cnn);
	}

	public void ExecuteNonQuery(IEasyFlowTransaction transaction, string sqlStatement, TimeSpan timeout)
	{
		var sqlTran = (MsSqlTransaction)transaction;
		Execute(sqlStatement, timeout, sqlTran.SqlTransaction.Connection);
	}

	private static void Execute(string sqlStatement, TimeSpan timeout, SqlConnection cnn)
	{
		ServerConnection serverCnn = new(cnn);
		//serverCnn.BeginTransaction(); //TODO: option for using transaction.

		serverCnn.StatementTimeout = (int)timeout.TotalSeconds;
		serverCnn.ExecuteNonQuery(sqlStatement);
	}
}