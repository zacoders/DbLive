namespace DbLive.Common;

public static class ExceptionExtensions
{
	public static TExceptionType? Get<TExceptionType>(this Exception ex)
	{
		if (ex is TExceptionType exception) return exception;

		if (ex.InnerException != null) return Get<TExceptionType>(ex.InnerException);

		return default;
	}
}
