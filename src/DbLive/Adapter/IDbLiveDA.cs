namespace DbLive.Adapter;

/// <summary>
/// The data access that the deployment and the tests use: the database, DbLive's own tables (<c>dblive</c>) and the code files.
/// Internal: a provider outside this repository is not supported.
/// </summary>
internal interface IDbLiveDA
{
	DbProvider Provider { get; }

	Task CreateDBAsync(bool skipIfExists = true);

	/// <summary>
	/// Drops the database of the connection string, also while other connections use it: SQL Server sets it to
	/// <c>single_user with rollback immediate</c>, PostgreSQL terminates its sessions. For the fixtures of <c>DbLive.xunit</c>
	/// and the internal option <see cref="DeployParameters.RecreateDatabase"/>.
	/// </summary>
	Task DropDbAsync(bool skipIfNotExists = true);

	/// <summary>
	/// Checks that the database of the connection string exists. Returns when it exists, and when the server
	/// does not tell: the next connection to the database then fails with the error of the provider.
	/// </summary>
	/// <exception cref="DatabaseNotFoundException">The server confirms that the database does not exist.</exception>
	Task CheckDatabaseExistsAsync();

	/// <summary>
	/// Takes the deployment lock of the database, so that one deployment of it runs at a time: an exclusive lock of the database,
	/// owned by the session of a connection of its own (SQL Server <c>sp_getapplock</c>, PostgreSQL <c>pg_try_advisory_lock</c>).
	/// It does not wait. The connection is not pooled, so its closing ends the session, and not enlisted in an ambient transaction.
	/// Disposing the result releases the lock and closes the connection; the server also releases it when the session ends.
	/// </summary>
	/// <exception cref="DeploymentInProgressException">Another session holds the lock.</exception>
	Task<IAsyncDisposable> AcquireDeploymentLockAsync();

	/// <summary>
	/// Executes a script. In an ambient transaction it fails with <see cref="DbLiveTransactionEndedException"/> when the script ended that
	/// transaction with COMMIT or ROLLBACK, and what the script ran after the end is rolled back; so do <see cref="ExecuteCodeItemAsync"/>
	/// and <see cref="ExecuteQueryMultipleAsync"/>, and a code file in a transaction of its own.
	/// </summary>
	/// <param name="sqlStatement">The script; on SQL Server with <c>GO</c> batches.</param>
	/// <param name="isolationLevel">The isolation level of the session when no ambient transaction runs; in one, the level of that transaction applies.</param>
	/// <param name="timeout">The timeout of the whole script, not of each of its commands; <see cref="TimeSpan.Zero"/> is no limit.</param>
	/// <exception cref="DbLiveSqlException"/>
	/// <exception cref="DbLiveTransactionEndedException">The script ended the ambient transaction.</exception>
	/// <exception cref="ExecutionTimeoutException">The script ran longer than <paramref name="timeout"/>.</exception>
	Task ExecuteNonQueryAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	);

	/// <param name="sqlStatement">The script; every result set it returns is an item of the result.</param>
	/// <param name="isolationLevel">The isolation level of the session when no ambient transaction runs; in one, the level of that transaction applies.</param>
	/// <param name="timeout">The timeout of the script; <see cref="TimeSpan.Zero"/> is no limit.</param>
	/// <exception cref="DbLiveSqlException"/>
	/// <exception cref="ExecutionTimeoutException">The script ran longer than <paramref name="timeout"/>.</exception>
	Task<List<SqlResult>> ExecuteQueryMultipleAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	);

	/// <summary>
	/// Reads the schema of the database from its catalog, for the schema check of the deployment test (<see cref="SchemaChangedException"/>).
	/// </summary>
	Task<SchemaSnapshot> GetSchemaSnapshotAsync();

	/// <summary>
	/// Called inside the ambient transaction after a script ran in it: true when later statements still run in that transaction.
	/// Has no side effects. On PostgreSQL an error aborts the transaction until the end of the transaction (<see cref="ExecuteCodeItemAsync"/>
	/// undoes that for its own error). On SQL Server most errors leave it open, but some roll it back: any run-time error with
	/// <c>XACT_ABORT ON</c>, a failed conversion, a deadlock. The next statements then do not fail, they run in autocommit mode
	/// and are committed at once, even when the deployment fails and is rolled back. A script can also end the transaction without
	/// an error, with COMMIT or ROLLBACK: <see cref="ExecuteNonQueryAsync"/> fails then. On PostgreSQL the answer relies on the ID of
	/// the transaction, which is assigned before every script in it: a transaction in which no script ran and nothing was written is
	/// not usable by this answer.
	/// </summary>
	Task<bool> IsTransactionUsableAsync();

	/// <summary>
	/// Executes a code file. Inside the ambient transaction, an error of the file leaves the transaction usable when the provider can,
	/// so that the file can be retried in it after the files it depends on; <see cref="IsTransactionUsableAsync"/> tells whether it did.
	/// On PostgreSQL the error also undoes the statements of the file that ran before it. On SQL Server they stay in the transaction,
	/// and an error that rolls back the whole transaction leaves it unusable. Outside a transaction the file runs in a transaction
	/// of its own, so it is applied as a whole: it runs between <c>begin</c> and <c>commit</c> on the connection, and fails when it ended
	/// that transaction (<see cref="ExecuteNonQueryAsync"/>). Except a file for which <see cref="RunsCodeItemWithoutTransaction"/>
	/// is true: its batches commit one by one.
	/// </summary>
	/// <param name="sqlStatement">The content of the code file; on SQL Server with <c>GO</c> batches.</param>
	/// <param name="isolationLevel">The isolation level of the session when no ambient transaction runs; in one, the level of that transaction applies.</param>
	/// <param name="timeout">The timeout of the whole file, not of each of its commands; <see cref="TimeSpan.Zero"/> is no limit.</param>
	/// <exception cref="DbLiveSqlException">The error of the file.</exception>
	/// <exception cref="ExecutionTimeoutException">The file ran longer than <paramref name="timeout"/>.</exception>
	Task ExecuteCodeItemAsync(
		string sqlStatement,
		TranIsolationLevel isolationLevel,
		TimeSpan timeout
	);

	/// <summary>
	/// True when <see cref="ExecuteCodeItemAsync"/>, outside a transaction, runs the file without a transaction of its own: the file has
	/// DDL that the server does not allow in a user transaction. On SQL Server a file with In-Memory OLTP DDL, found by its words
	/// <c>native_compilation</c> and <c>memory_optimized</c>; on PostgreSQL never. Read from the text only, without a connection.
	/// </summary>
	bool RunsCodeItemWithoutTransaction(string sqlStatement);

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
