namespace EasyFlow;


internal static class TransactionExtentions
{
	public static IsolationLevel ToSystemTransaction(this TranIsolationLevel transaction)
		=> transaction switch
		{
			TranIsolationLevel.Chaos => IsolationLevel.Chaos,
			TranIsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
			TranIsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
			TranIsolationLevel.Serializable => IsolationLevel.Serializable,
			_ => throw new NotSupportedException()
		};
}

