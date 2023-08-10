namespace EasyFlow.Deploy.MSSQL;

public class Deployer : IEasyFlowDeployer
{
	public IEasyFlowTransaction BeginTransaction()
	{
		throw new NotImplementedException();
	}

	public void EndTransaction(IEasyFlowTransaction transaction)
	{
		throw new NotImplementedException();
	}

	public void ExecuteNonQuery(string sql)
	{
		throw new NotImplementedException();
	}
}