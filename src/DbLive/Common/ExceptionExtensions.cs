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
	/// </summary>
	internal static string GetSqlErrorOrMessage(this Exception ex)
	{
		return ex is DbLiveSqlException { SqlError: string sqlError } && !string.IsNullOrWhiteSpace(sqlError)
			? sqlError.TrimEnd()
			: ex.Message;
	}
}
