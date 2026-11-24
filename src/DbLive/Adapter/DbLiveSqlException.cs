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
	/// <c>GO</c> batch starts. Empty for a timeout, and on PostgreSQL for a lost connection: the message has the cause then.
	/// On SQL Server a lost connection or a killed session is a <c>SqlException</c> like any other error, and this holds its errors.
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

	/// <summary>
	/// A value of a result set of a SQL test that the provider cannot read. The error of the provider names neither the column nor
	/// what to do. <paramref name="resultSetNumber"/>: the number of the result set among those of the script, from 1.
	/// </summary>
	internal static DbLiveSqlException CannotReadValue(SqlColumn column, int rowNumber, int resultSetNumber, Exception error) => new(
		$"""
		Cannot read the value of the column '{column.ColumnName}' in row {rowNumber} of result set {resultSetNumber}, of the type {column.DataType}: {error.Message}
		Select the value as text in the test.
		""",
		"",
		error
	);
}
