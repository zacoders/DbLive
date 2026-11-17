namespace DbLive;

/// <summary>
/// Another deployment of the database is running: DbLive deploys a database one deployment at a time.
/// The deployment stops before it reads or changes anything in the database.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DeploymentInProgressException : Exception
{
	internal DeploymentInProgressException(string databaseName, string server)
		: base(
			$"Another deployment of database '{databaseName}' on server '{server}' is running: DbLive deploys a database one deployment at a time, "
			+ "and this deployment stopped before it changed anything. Run it again when the other one ends, and let the pipeline run one deployment "
			+ "of the database at a time. When no deployment runs, the lock is held by the session of one that lost its connection: "
			+ "see \"One deployment at a time\" in the DbLive docs."
		)
	{
		DatabaseName = databaseName;
		Server = server;
	}

	public string DatabaseName { get; }

	public string Server { get; }
}
