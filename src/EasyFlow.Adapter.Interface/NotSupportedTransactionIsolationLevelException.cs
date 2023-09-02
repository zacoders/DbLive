namespace EasyFlow.Adapter.Interface;

[Serializable]
public class NotSupportedTransactionIsolationLevelException : Exception
{
	private TranIsolationLevel isolationLevel;

	public NotSupportedTransactionIsolationLevelException(TranIsolationLevel isolationLevel)
		: base($"Unsupported transaction isolation level '{isolationLevel}'.")
	{
		this.isolationLevel = isolationLevel;
	}
}