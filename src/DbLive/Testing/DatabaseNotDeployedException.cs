namespace DbLive.Testing;

/// <summary>
/// The SQL tests were started against a database that has no DbLive tables.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class DatabaseNotDeployedException : Exception
{
	internal DatabaseNotDeployedException()
		: base("The database has no DbLive tables: the project was not deployed to it. Deploy the project before running the SQL tests.")
	{
	}
}
