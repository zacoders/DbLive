namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDeployer : IEasyFlowDeployer
{
	public void BeginTransaction(IEasyFlowSqlConnection cnn, TransactionIsolationLevel isolationLevel)
	{
		throw new NotImplementedException();
	}

	public void CommitTransaction(IEasyFlowSqlConnection cnn)
	{
		throw new NotImplementedException();
	}

	public void ExecuteNonQuery(IEasyFlowSqlConnection cnn, string sqlStatementt)
	{
		throw new NotImplementedException();
	}

	public IEasyFlowSqlConnection OpenConnection(string cnnString)
	{
		throw new NotImplementedException();
	}
}