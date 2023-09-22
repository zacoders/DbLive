namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	
	bool EasyFlowInstalled(string cnnString);
	
	int GetEasyFlowVersion(string cnnString);

	void SetEasyFlowVersion(string cnnString, int version, DateTime migrationCompletedUtc);

	/// <exception cref="EasyFlowSqlException"/>
	void MigrationCompleted(string cnnString, int migrationVerion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc);
}
