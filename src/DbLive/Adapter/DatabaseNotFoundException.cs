namespace DbLive;

/// <summary>
/// The database of the connection string does not exist.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DatabaseNotFoundException : Exception
{
	internal DatabaseNotFoundException(string databaseName, string server, Exception innerException)
		: base(
			$"Database '{databaseName}' does not exist on server '{server}'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
			innerException
		)
	{
		DatabaseName = databaseName;
		Server = server;
	}

	/// <summary>
	/// The database name of the connection string.
	/// </summary>
	public string DatabaseName { get; }

	/// <summary>
	/// The server of the connection string.
	/// </summary>
	public string Server { get; }
}
