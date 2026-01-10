
namespace DbLive.Adapter;

public interface IDbLiveDA
{
	Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync();

	Task<long?> GetMigrationHashAsync(int version, MigrationItemType itemType);

	Task<int> GetCurrentMigrationVersionAsync();

	Task<bool> DbLiveInstalledAsync();

	Task<int> GetDbLiveVersionAsync();

	Task SetDbLiveVersionAsync(int version, DateTime migrationDatetime);

	/// <exception cref="DbLiveSqlException"/>
	Task SetCurrentMigrationVersionAsync(int version, DateTime migrationCompletedUtc);

	/// <exception cref="DbLiveSqlException"/>
	Task SaveCodeItemAsync(CodeItemDto item);

	Task MarkCodeAsVerifiedAsync(string relativePath, DateTime verifiedUtc);

	Task CreateDBAsync(bool skipIfExists = true);

	Task DropDBAsync(bool skipIfNotExists = true);

	/// <exception cref="DbLiveSqlException"/>
	Task ExecuteNonQueryAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	);

	Task<List<SqlResult>> ExecuteQueryMultipleAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	);

	Task<CodeItemDto?> FindCodeItemAsync(string relativePath);

	Task SaveMigrationItemAsync(MigrationItemSaveDto item);

	/// <summary>
	/// Updates the migration state for the specified migration item.
	/// </summary>
	/// <exception cref="DbLiveMigrationItemMissedSqlException"></exception>
	Task UpdateMigrationStateAsync(MigrationItemStateDto item);

	Task<bool> MigrationItemExistsAsync(int version, MigrationItemType itemType);

	Task<string?> GetMigrationContentAsync(int version, MigrationItemType undo);

	Task SaveUnitTestResultAsync(UnitTestItemDto item);

	Task MarkItemAsAppliedAsync(
		ProjectFolder projectFolder,
		string relativePath,
		DateTime startedUtc,
		DateTime completedUtc,
		long executionTimeMs,
		long contentHash
	);
}
