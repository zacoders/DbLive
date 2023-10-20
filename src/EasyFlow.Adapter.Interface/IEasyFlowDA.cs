namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);

	bool EasyFlowInstalled(string cnnString);

	int GetEasyFlowVersion(string cnnString);

	void SetEasyFlowVersion(string cnnString, int version, DateTime migrationDatetime);

	//TODO: add hash of the migration. Can be needed in the future, just to check applied and existing file content.
	/// <exception cref="EasyFlowSqlException"/>
	void MarkMigrationAsApplied(string cnnString, int migrationVersion, string migrationName, DateTime migrationStartedUtc, DateTime migrationCompletedUtc);

	/// <exception cref="EasyFlowSqlException"/>
	void MarkCodeAsApplied(string cnnString, string relativePath, Guid contentMD5Hash, DateTime createdUtc, int executionTimeMs);
	void MarkCodeAsVerified(string cnnString, string relativePath, DateTime verifiedUtc);

	void CreateDB(string cnnString, bool skipIfExists = true);

	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string cnnString, string sqlStatementt);
	bool IsCodeItemApplied(string cnnString, string filePath, Guid mD5Hash);
}
