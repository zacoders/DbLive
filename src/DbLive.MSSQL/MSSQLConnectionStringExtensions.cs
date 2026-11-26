namespace DbLive.MSSQL;

/// <summary>
/// Replaces the database name of a SQL Server connection string, for test databases.
/// </summary>
public static class MSSQLConnectionStringExtensions
{
	/// <summary>
	/// The connection string with another database name (<c>Initial Catalog</c>).
	/// </summary>
	/// <param name="mssqlConnectionString">A SQL Server connection string.</param>
	/// <param name="dbName">The database name.</param>
	/// <returns>A new connection string; the other keys stay.</returns>
	public static string SetMsSqlDatabaseName(this string mssqlConnectionString, string dbName)
	{
		SqlConnectionStringBuilder cnnBuilder = new(mssqlConnectionString)
		{
			InitialCatalog = dbName
		};
		return cnnBuilder.ConnectionString;
	}

	/// <summary>
	/// The connection string with a new random database name, <c>DbLive--&lt;guid&gt;</c>.
	/// </summary>
	/// <param name="mssqlConnectionString">A SQL Server connection string.</param>
	/// <returns>A new connection string; the other keys stay.</returns>
	public static string SetRandomMsSqlDatabaseName(this string mssqlConnectionString)
	{
		return mssqlConnectionString.SetMsSqlDatabaseName($"DbLive--{Guid.NewGuid()}");
	}
}
