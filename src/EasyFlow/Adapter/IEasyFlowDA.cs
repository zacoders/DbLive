
namespace EasyFlow.Adapter;

public interface IEasyFlowDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations();
	IReadOnlyCollection<MigrationItemDto> GetNonAppliedBreakingMigrationItems();

	bool EasyFlowInstalled();

	int GetEasyFlowVersion();

	void SetEasyFlowVersion(int version, DateTime migrationDatetime);

	/// <exception cref="EasyFlowSqlException"/>
	void SaveMigration(int migrationVersion, string migrationName, DateTime migrationCompletedUtc);

	/// <exception cref="EasyFlowSqlException"/>
	void MarkCodeAsApplied(string relativePath, int crc32Hash, DateTime createdUtc, long executionTimeMs);
	void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc);

	void CreateDB(bool skipIfExists = true);

	void DropDB(bool skipIfNotExists = true);

	/// <exception cref="EasyFlowSqlException"/>
	void ExecuteNonQuery(string sqlStatementt);
	CodeItemDto? FindCodeItem(string relativePath);
	void SaveMigrationItemState(MigrationItemDto item);
	void SaveUnitTestResult(UnitTestItemDto item);
	void MarkItemAsApplied(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs);
}
