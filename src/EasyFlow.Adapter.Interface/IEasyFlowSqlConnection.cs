namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowSqlConnection
{
	void BeginTransaction(TransactionIsolationLevel isolationLevel);
	void CommitTransaction();
	void ExecuteNonQuery(string sqlStatementt);
}
