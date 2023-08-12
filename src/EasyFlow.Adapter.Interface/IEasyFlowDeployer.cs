namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDeployer
{
	IEasyFlowSqlConnection OpenConnection(string cnnString);
	void BeginTransaction(IEasyFlowSqlConnection cnn, TransactionIsolationLevel isolationLevel);
	void CommitTransaction(IEasyFlowSqlConnection cnn);
	void ExecuteNonQuery(IEasyFlowSqlConnection cnn, string sqlStatementt);
}