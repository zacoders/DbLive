namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	
	bool EasyFlowInstalled(string cnnString);
	
	int GetEasyFlowVersion(string cnnString);

	void SetEasyFlowVersion(string cnnString, int version, DateTime migrationDatetime);

	/// <exception cref="EasyFlowSqlException"/>
	void MigrationCompleted(string cnnString, int migrationVersion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc);

	void CreateDB(string cnnString, bool skipIfExists = true);

	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string cnnString, string sqlStatementt);
}
