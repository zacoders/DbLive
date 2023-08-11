namespace EasyFlow.Adapter.PostgreSQL;

public class PostgreSqlDeployer : IEasyFlowDeployer
{
	public IEasyFlowTransaction BeginTransaction(string cnnString, TransactionIsolationLevel isolationLevel)
	{
		throw new NotImplementedException();
	}

	public void EndTransaction(IEasyFlowTransaction transaction)
	{
		throw new NotImplementedException();
	}

	public void ExecuteNonQuery(IEasyFlowTransaction transaction, string sqlStatement, TimeSpan timeout)
	{
		throw new NotImplementedException();
	}

	public void ExecuteNonQuery(string cnnString, string sqlStatement, TimeSpan timeout)
	{
		throw new NotImplementedException();
	}

	public IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString)
	{
		throw new NotImplementedException();
	}
}