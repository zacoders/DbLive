namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
internal class DbLiveMigrationItemMissedSqlException : Exception
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
