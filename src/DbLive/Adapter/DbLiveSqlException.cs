namespace DbLive;

[Serializable]
[ExcludeFromCodeCoverage]
public sealed class DbLiveSqlException : Exception
{
	public string SqlError { get; internal set; }

	internal DbLiveSqlException(string errorMessage, string sqlError, Exception innerException)
		: base(errorMessage, innerException)
	{
		SqlError = sqlError;
	}

	internal DbLiveSqlException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
		SqlError = "";
	}

	internal DbLiveSqlException(string errorMessage)
		: base(errorMessage)
	{
		SqlError = "";
	}
}
