namespace EasyFlow.Adapter;

[Serializable]
public class NotSupportedTransactionIsolationLevelException : Exception
{
	private TransactionIsolationLevel isolationLevel;

	public NotSupportedTransactionIsolationLevelException(TransactionIsolationLevel isolationLevel)
		: base($"Unsupported transaction isolation level '{isolationLevel}'.")
	{
		this.isolationLevel = isolationLevel;
	}
}