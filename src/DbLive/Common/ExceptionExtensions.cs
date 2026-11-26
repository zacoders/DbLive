namespace DbLive.Common;

internal static class ExceptionExtensions
{
	public static TExceptionType? Get<TExceptionType>(this Exception ex)
	{
		if (ex is TExceptionType exception) return exception;

		if (ex.InnerException != null) return Get<TExceptionType>(ex.InnerException);

		return default;
	}

	/// <summary>
	/// The error of the server (SQL Server: Msg, Level, State, Line and the message of each error), or the message of the exception
	/// when the error does not come from the server. No stack traces: they stay in the exception and in the log.
	/// An error of the server at the commit of a <see cref="TransactionScope"/> (on PostgreSQL a deferred constraint, checked at
	/// <c>COMMIT</c>) is a <see cref="TransactionAbortedException"/>, "The transaction has aborted.", with the error inside: its message
	/// follows on the next line.
	/// </summary>
	internal static string GetSqlErrorOrMessage(this Exception ex)
	{
		if (ex is TransactionAbortedException { InnerException: Exception cause })
		{
			return $"{ex.Message}{Environment.NewLine}{cause.GetSqlErrorOrMessage()}";
		}

		return ex is DbLiveSqlException { SqlError: string sqlError } && !string.IsNullOrWhiteSpace(sqlError)
			? sqlError.TrimEnd()
			: ex.Message;
	}

	/// <summary>
	/// The message of the error that names a failed file: <paramref name="message"/>, then the error of the file on the next lines
	/// (<see cref="GetSqlErrorOrMessage"/>). <see cref="DbLiveSqlException.SqlError"/> is a property, not a part of the message: without
	/// this the log and the message would have the message of the server only, without its number and lines.
	/// </summary>
	internal static string WithSqlErrorOrMessage(this Exception ex, string message) =>
		$"{message}{Environment.NewLine}{ex.GetSqlErrorOrMessage()}";
}
