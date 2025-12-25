
namespace DbLive.Adapter;

public interface IDbLiveDA
{
	IReadOnlyCollection<MigrationDto> GetMigrations();
	IReadOnlyCollection<MigrationItemDto> GetNonAppliedBreakingMigrationItems();

	bool DbLiveInstalled();

	int GetDbLiveVersion();

	void SetDbLiveVersion(int version, DateTime migrationDatetime);

	/// <exception cref="DbLiveSqlException"/>
	void SaveMigration(int migrationVersion, DateTime migrationCompletedUtc);

	/// <exception cref="DbLiveSqlException"/>
	void MarkCodeAsApplied(string relativePath, int crc32Hash, DateTime createdUtc, long executionTimeMs);
	void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc);

	void CreateDB(bool skipIfExists = true);

	void DropDB(bool skipIfNotExists = true);

	/// <exception cref="DbLiveSqlException"/>
	void ExecuteNonQuery(string sqlStatementt);

	List<SqlResult> ExecuteQueryMultiple(string sqlStatement);

	CodeItemDto? FindCodeItem(string relativePath);
	void SaveMigrationItemState(MigrationItemDto item);
	void SaveUnitTestResult(UnitTestItemDto item);
	void MarkItemAsApplied(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs);
}
