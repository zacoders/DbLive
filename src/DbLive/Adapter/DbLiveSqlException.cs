namespace DbLive.Adapter;

[Serializable]
[ExcludeFromCodeCoverage]
public class DbLiveSqlException : Exception
{
	public string SqlError { get; set; }

	public DbLiveSqlException(string errorMessage, string sqlError, Exception innerException)
		: base(errorMessage, innerException)
	{
		SqlError = sqlError;
	}

	public DbLiveSqlException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
		SqlError = "";
	}

	public DbLiveSqlException(string errorMessage)
		: base(errorMessage)
	{
		SqlError = "";
	}
}
