
namespace DbLive.PostgreSQL;

/// <summary>
/// Replaces the database name of a PostgreSQL connection string, for test databases.
/// </summary>
public static class PostgreSqlConnectionStringExtensions
{
	/// <summary>
	/// The connection string with another database name (<c>Database</c>).
	/// </summary>
	/// <param name="pgConnectionString">A PostgreSQL connection string.</param>
	/// <param name="dbName">The database name.</param>
	/// <returns>A new connection string; the other keys stay.</returns>
	public static string SetPostgreSqlDatabaseName(this string pgConnectionString, string dbName)
	{
		NpgsqlConnectionStringBuilder cnnBuilder = new(pgConnectionString)
		{
			Database = dbName
		};
		return cnnBuilder.ConnectionString;
	}

	/// <summary>
	/// The connection string with a new random database name, <c>dblive--&lt;guid&gt;</c>.
	/// </summary>
	/// <param name="pgConnectionString">A PostgreSQL connection string.</param>
	/// <returns>A new connection string; the other keys stay.</returns>
	public static string SetRandomPostgreSqlDatabaseName(this string pgConnectionString)
	{
		return pgConnectionString.SetPostgreSqlDatabaseName($"dblive--{Guid.NewGuid()}");
	}
}