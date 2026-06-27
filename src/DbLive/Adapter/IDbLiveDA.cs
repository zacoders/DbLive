
namespace DbLive.Adapter;

public interface IDbLiveDA
{
	Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync();

	Task<long?> GetMigrationHashAsync(long version, MigrationItemType itemType);

	Task<IReadOnlyCollection<long>> GetAppliedMigrationVersionsAsync();

	Task<bool> DbLiveInstalledAsync();

	Task<long> GetDbLiveVersionAsync();

	Task SetDbLiveVersionAsync(long version, DateTime migrationDatetime);

	Task<string?> GetProjectIdAsync();

	Task SetProjectIdAsync(string projectId);

	/// <exception cref="DbLiveSqlException"/>
	Task SaveCodeItemAsync(CodeItemDto item);

	Task MarkCodeAsVerifiedAsync(string relativePath, DateTime verifiedUtc);

	Task CreateDBAsync(bool skipIfExists = true);

	Task DropDbAsync(bool skipIfNotExists = true);

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

	Task<bool> MigrationItemExistsAsync(long version, MigrationItemType itemType);

	Task<string?> GetMigrationContentAsync(long version, MigrationItemType undo);

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
