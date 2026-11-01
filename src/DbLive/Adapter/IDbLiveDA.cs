
namespace DbLive.Adapter;

public interface IDbLiveDA
{
	DbProvider Provider { get; }

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

	/// <summary>
	/// Marks the rows of <c>dblive.code</c> whose file is not one of <paramref name="projectRelativePaths"/> as removed,
	/// with <paramref name="verifiedUtc"/> as the time the file was found missing. Rows already removed are not changed.
	/// </summary>
	/// <returns>The stored (lower-case) paths of the rows marked now.</returns>
	Task<IReadOnlyCollection<string>> MarkMissingCodeAsRemovedAsync(IReadOnlyCollection<string> projectRelativePaths, DateTime verifiedUtc);

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

	Task RepairMigrationChecksumAsync(MigrationChecksumRepairDto item);

	/// <summary>
	/// Updates the migration state for the specified migration item.
	/// </summary>
	/// <exception cref="DbLiveMigrationItemMissedSqlException"></exception>
	Task UpdateMigrationStateAsync(MigrationItemStateDto item);

	Task<bool> MigrationItemExistsAsync(long version, MigrationItemType itemType);

	Task<string?> GetMigrationContentAsync(long version, MigrationItemType undo);

	Task SaveUnitTestResultAsync(UnitTestItemDto item);

	/// <summary>
	/// Reads the schema of the database from its catalog, for the schema checks of the deployment tests (<see cref="SchemaCheck"/>).
	/// </summary>
	Task<SchemaSnapshot> GetSchemaSnapshotAsync();

	Task MarkItemAsAppliedAsync(
		ProjectFolder projectFolder,
		string relativePath,
		DateTime startedUtc,
		DateTime completedUtc,
		long executionTimeMs,
		long contentHash
	);
}
