namespace EasyFlow.Adapter.Interface;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations(string cnnString);
	IReadOnlyCollection<MigrationItemDto> GetNonAppliedBreakingMigrationItems(string cnnString);

	bool EasyFlowInstalled(string cnnString);

	int GetEasyFlowVersion(string cnnString);

	void SetEasyFlowVersion(string cnnString, int version, DateTime migrationDatetime);

	/// <exception cref="EasyFlowSqlException"/>
	void SaveMigration(string cnnString, int migrationVersion, string migrationName, DateTime migrationCompletedUtc);

	/// <exception cref="EasyFlowSqlException"/>
	void MarkCodeAsApplied(string cnnString, string relativePath, int crc32Hash, DateTime createdUtc, int executionTimeMs);
	void MarkCodeAsVerified(string cnnString, string relativePath, DateTime verifiedUtc);

	void CreateDB(string cnnString, bool skipIfExists = true);

	void DropDB(string cnnString, bool skipIfNotExists = true);

	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string cnnString, string sqlStatementt);
	CodeItemDto? FindCodeItem(string cnnString, string relativePath);
	void SaveMigrationItemState(string cnnString, MigrationItemDto item);
	void SaveUnitTestResult(string cnnString, string relativePath, int crc32Hash, DateTime startedUtc, int executionTimeMs, bool isSuccess, string? errorMessage);
}
