namespace DbLive.Adapter;

/// <summary>
/// The data access that the deployment and the tests use: DbLive's own tables (<c>dblive</c>) and the code files.
/// Internal: a provider outside this repository is not supported. <see cref="IDbLiveDA"/> is the public part.
/// </summary>
internal interface IDbLiveInternalDA : IDbLiveDA
{
	/// <summary>
	/// Called inside the ambient transaction: true when later statements still run in that transaction. Has no side effects.
	/// On PostgreSQL an error aborts the transaction until the end of the transaction (<see cref="ExecuteCodeItemAsync"/> undoes
	/// that for its own error). On SQL Server most errors leave it open, but some roll it back: any run-time error with
	/// <c>XACT_ABORT ON</c>, a failed conversion, a deadlock. The next statements then do not fail, they run in autocommit mode
	/// and are committed at once, even when the deployment fails and is rolled back.
	/// </summary>
	Task<bool> IsTransactionUsableAsync();

	/// <summary>
	/// Executes a code file. Inside the ambient transaction, an error of the file leaves the transaction usable when the provider can,
	/// so that the file can be retried in it after the files it depends on; <see cref="IsTransactionUsableAsync"/> tells whether it did.
	/// On PostgreSQL the error also undoes the statements of the file that ran before it. On SQL Server they stay in the transaction,
	/// and an error that rolls back the whole transaction leaves it unusable. Outside a transaction: <see cref="IDbLiveDA.ExecuteNonQueryAsync"/>.
	/// </summary>
	/// <exception cref="DbLiveSqlException">The error of the file.</exception>
	Task ExecuteCodeItemAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel = TranIsolationLevel.ReadCommitted,
		TimeSpan? timeout = null
	);

	Task<IReadOnlyCollection<MigrationItemDto>> GetMigrationsAsync();

	Task<IReadOnlyCollection<long>> GetAppliedMigrationVersionsAsync();

	Task<bool> DbLiveInstalledAsync();

	/// <summary>
	/// The number of the last self-migration applied to the <c>dblive</c> schema, from <c>dblive.version.version</c>.
	/// Not a DbLive release version: that is <see cref="GetMinDbLiveLibVersionAsync"/>.
	/// </summary>
	Task<long> GetSelfMigrationVersionAsync();

	/// <summary>
	/// Stores the number of the last self-migration applied in <c>dblive.version.version</c>.
	/// </summary>
	Task SetSelfMigrationVersionAsync(long version, DateTime migrationDatetime);

	/// <summary>
	/// The oldest DbLive version that can deploy the database, from <c>dblive.version.min_dblive_lib_version</c>.
	/// Null when there is no requirement, also when the column does not exist yet (a database deployed before the
	/// self-migration that adds it). Call it only when the database has the DbLive tables (<see cref="DbLiveInstalledAsync"/>).
	/// </summary>
	Task<Version?> GetMinDbLiveLibVersionAsync();

	/// <summary>
	/// Stores the oldest DbLive version that can deploy the database in <c>dblive.version.min_dblive_lib_version</c>.
	/// </summary>
	Task SetMinDbLiveLibVersionAsync(Version version);

	/// <summary>
	/// The ProjectId bound to the database, from <c>dblive.version.project_id</c>. Null when there is none, also when the column
	/// does not exist yet (a database deployed before the self-migration that adds it). Call it only when the database has the
	/// DbLive tables (<see cref="DbLiveInstalledAsync"/>).
	/// </summary>
	Task<string?> GetProjectIdAsync();

	Task SetProjectIdAsync(string projectId);

	/// <exception cref="DbLiveSqlException"/>
	Task SaveCodeItemAsync(CodeItemDto item);

	/// <summary>
	/// Sets <c>verified_utc</c> of the rows of <c>dblive.code</c> of <paramref name="relativePaths"/>, with one command.
	/// </summary>
	Task MarkCodeAsVerifiedAsync(IReadOnlyCollection<string> relativePaths, DateTime verifiedUtc);

	/// <summary>
	/// Marks the rows of <c>dblive.code</c> whose file is not one of <paramref name="projectRelativePaths"/> as removed,
	/// with <paramref name="verifiedUtc"/> as the time the file was found missing. Rows already removed are not changed.
	/// </summary>
	/// <returns>The stored (lower-case) paths of the rows marked now.</returns>
	Task<IReadOnlyCollection<string>> MarkMissingCodeAsRemovedAsync(IReadOnlyCollection<string> projectRelativePaths, DateTime verifiedUtc);

	/// <summary>
	/// All rows of <c>dblive.code</c>, with one query.
	/// </summary>
	Task<IReadOnlyCollection<CodeItemDto>> GetCodeItemsAsync();

	Task SaveMigrationItemAsync(MigrationItemSaveDto item);

	Task RepairMigrationChecksumAsync(MigrationChecksumRepairDto item);

	/// <summary>
	/// Updates the migration state for the specified migration item.
	/// </summary>
	/// <exception cref="DbLiveMigrationItemMissedSqlException"></exception>
	Task UpdateMigrationStateAsync(MigrationItemStateDto item);

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
