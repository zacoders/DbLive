namespace DbLive.Adapter;

[ExcludeFromCodeCoverage]
public class DbLiveSqlException : Exception
{
	public DbLiveSqlException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}
	public DbLiveSqlException(string errorMessage)
		: base(errorMessage)
	{
	}
}
