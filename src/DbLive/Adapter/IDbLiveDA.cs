
namespace DbLive.Adapter;

/// <summary>
/// Access to the database of the connection string: create and drop it, run SQL, read its schema.
/// What DbLive writes to its own tables (<c>dblive</c>) is internal.
/// </summary>
public interface IDbLiveDA
{
	DbProvider Provider { get; }

	Task CreateDBAsync(bool skipIfExists = true);

	/// <summary>
	/// Checks that the database of the connection string exists. Returns when it exists, and when the server
	/// does not tell: the next connection to the database then fails with the error of the provider.
	/// </summary>
	/// <exception cref="DatabaseNotFoundException">The server confirms that the database does not exist.</exception>
	Task CheckDatabaseExistsAsync();

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

	/// <summary>
	/// Reads the schema of the database from its catalog, for the schema check of the deployment test (<see cref="SchemaChangedException"/>).
	/// </summary>
	Task<SchemaSnapshot> GetSchemaSnapshotAsync();
}
