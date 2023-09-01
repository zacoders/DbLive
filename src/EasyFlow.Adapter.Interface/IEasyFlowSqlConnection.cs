namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowSqlConnection : IDisposable
{
	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string sqlStatementt);

	/// <exception cref="EasyFlowSqlException"/>
	void Close();

	/// <exception cref="EasyFlowSqlException"/>
	void MigrationCompleted(string domain, int migrationVerion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc);
}
