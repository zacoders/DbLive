namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDeployer
{
	/// <exception cref="NotSupportedTransactionIsolationLevelException"/>
	IEasyFlowTransaction BeginTransaction(string cnnString, TransactionIsolationLevel isolationLevel);

	void ExecuteNonQuery(IEasyFlowTransaction transaction, string sqlStatement, TimeSpan timeout);

	void ExecuteNonQuery(string cnnString, string sqlStatement, TimeSpan timeout);
}