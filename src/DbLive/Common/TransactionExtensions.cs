namespace DbLive.Common;

using System.Transactions;

internal static class TransactionExtensions
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
