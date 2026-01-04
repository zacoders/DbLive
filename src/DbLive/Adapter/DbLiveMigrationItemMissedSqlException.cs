namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class DbLiveMigrationItemMissedSqlException : Exception
{
	public DbLiveMigrationItemMissedSqlException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}
	public DbLiveMigrationItemMissedSqlException(string errorMessage)
		: base(errorMessage)
	{
	}
}
