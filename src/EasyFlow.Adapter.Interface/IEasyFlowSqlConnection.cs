namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowSqlConnection
{
	/// <exception cref="EasyFlowSqlException"/>
	void BeginTransaction(TransactionIsolationLevel isolationLevel);

	/// <exception cref="EasyFlowSqlException"/>
	void CommitTransaction();

	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string sqlStatementt);

	/// <exception cref="EasyFlowSqlException"/>
	void Close();

	/// <exception cref="EasyFlowSqlException"/>
	void MigrationCompleted(int migrationVerion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc);
}
