namespace DbLive;

/// <summary>
/// A file of the Code folder failed. The message names it on its first line and holds the error of the file on the next lines: the
/// <see cref="DbLiveSqlException.SqlError"/> of the server, or the message of an error that does not come from the server. Thrown for a
/// breaking script of the Code folder, after which the breaking scripts that follow do not run; a code file's is an inner exception of
/// <see cref="CodeDeploymentAggregateException"/>. The inner exception is the error of the file, usually a <see cref="DbLiveSqlException"/>.
/// A failed breaking script keeps the row of its last success in the <c>dblive</c> tables. A failed code file keeps the
/// <c>applying</c> row written before it ran, with the hash of its last success, and runs on the next deployment whatever its content.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class CodeDeploymentException : Exception
{
	internal CodeDeploymentException(string errorMessage, Exception innerException)
		: base(errorMessage, innerException)
	{
	}
}
