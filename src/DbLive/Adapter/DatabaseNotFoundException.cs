namespace DbLive.Adapter;

/// <summary>
/// The database of the connection string does not exist.
/// </summary>
[ExcludeFromCodeCoverage]
public class DatabaseNotFoundException(string databaseName, string server, Exception innerException)
	: Exception(
		$"Database '{databaseName}' does not exist on server '{server}'. Check the database name in the connection string. A deployment creates the database only with DeployParameters.CreateDbIfNotExists = true.",
		innerException
	)
{
	public string DatabaseName { get; } = databaseName;

	public string Server { get; } = server;
}
