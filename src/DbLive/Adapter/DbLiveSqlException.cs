namespace DbLive;

/// <summary>
/// A SQL error of a script, or an error that does not come from the server: a timeout, a lost connection. It is the inner exception
/// of the deployment error that names the file (<see cref="MigrationDeploymentException"/>, <see cref="CodeDeploymentException"/>,
/// <see cref="FolderDeploymentException"/>), and the exception of a failed SQL test. The provider's exception is its inner exception.
/// </summary>
[Serializable]
[ExcludeFromCodeCoverage]
public class DbLiveSqlException : Exception
{
	/// <summary>
	/// The details of the server's error: number, severity, line; on SQL Server also the line of the file where the failed
	/// <c>GO</c> batch starts. Empty when the error does not come from the server, such as a timeout or a lost connection:
	/// the message has the cause then.
	/// </summary>
	public string SqlError { get; internal set; }

	internal DbLiveSqlException(string errorMessage, string sqlError, Exception? innerException)
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
