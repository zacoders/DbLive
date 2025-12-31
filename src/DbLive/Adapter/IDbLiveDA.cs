
namespace DbLive.Adapter;

public interface IDbLiveDA
{
	IReadOnlyCollection<MigrationItemDto> GetMigrations();

	int GetCurrentMigrationVersion();

	bool DbLiveInstalled();

	int GetDbLiveVersion();

	void SetDbLiveVersion(int version, DateTime migrationDatetime);

	/// <exception cref="DbLiveSqlException"/>
	void SaveCurrentMigrationVersion(int version, DateTime migrationCompletedUtc);

	/// <exception cref="DbLiveSqlException"/>
	void SaveCodeItem(CodeItemDto item);

	void MarkCodeAsVerified(string relativePath, DateTime verifiedUtc);

	void CreateDB(bool skipIfExists = true);

	void DropDB(bool skipIfNotExists = true);

	/// <exception cref="DbLiveSqlException"/>
	void ExecuteNonQuery(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	);

	List<SqlResult> ExecuteQueryMultiple(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	);

	CodeItemDto? FindCodeItem(string relativePath);
	void SaveMigrationItemState(MigrationItemDto item);
	void SaveUnitTestResult(UnitTestItemDto item);
	void MarkItemAsApplied(ProjectFolder projectFolder, string relativePath, DateTime startedUtc, DateTime completedUtc, long executionTimeMs);
}
