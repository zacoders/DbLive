namespace DbLive;

[ExcludeFromCodeCoverage]
public sealed class NotSupportedTransactionIsolationLevelException : Exception
{
	private TranIsolationLevel isolationLevel;

	internal NotSupportedTransactionIsolationLevelException(TranIsolationLevel isolationLevel)
		: base($"Unsupported transaction isolation level '{isolationLevel}'.")
	{
		this.isolationLevel = isolationLevel;
	}
}