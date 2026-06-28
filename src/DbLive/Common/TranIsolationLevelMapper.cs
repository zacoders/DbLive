namespace DbLive.Common;

internal static class TranIsolationLevelMapper
{
	public static IsolationLevel ToSystemTransaction(TranIsolationLevel level)
		=> level switch
		{
			TranIsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
			TranIsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
			TranIsolationLevel.Serializable => IsolationLevel.Serializable,
			TranIsolationLevel.Snapshot => IsolationLevel.Snapshot,
			_ => throw new NotSupportedTransactionIsolationLevelException(level)
		};

	public static string ToSql(TranIsolationLevel level, DbProvider provider)
	{
		ValidateForProvider(level, provider);

		return provider switch
		{
			DbProvider.MsSql => level switch
			{
				TranIsolationLevel.ReadCommitted => "read committed",
				TranIsolationLevel.RepeatableRead => "repeatable read",
				TranIsolationLevel.Serializable => "serializable",
				TranIsolationLevel.Snapshot => "snapshot",
				_ => throw new NotSupportedTransactionIsolationLevelException(level)
			},
			DbProvider.PostgreSql => level switch
			{
				TranIsolationLevel.ReadCommitted => "read committed",
				TranIsolationLevel.RepeatableRead => "repeatable read",
				TranIsolationLevel.Serializable => "serializable",
				_ => throw new NotSupportedTransactionIsolationLevelException(level)
			},
			_ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
		};
	}

	public static IReadOnlyCollection<TranIsolationLevel> GetSupportedLevels(DbProvider provider)
		=> provider switch
		{
			DbProvider.MsSql =>
			[
				TranIsolationLevel.ReadCommitted,
				TranIsolationLevel.RepeatableRead,
				TranIsolationLevel.Serializable,
				TranIsolationLevel.Snapshot
			],
			DbProvider.PostgreSql =>
			[
				TranIsolationLevel.ReadCommitted,
				TranIsolationLevel.RepeatableRead,
				TranIsolationLevel.Serializable
			],
			_ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
		};

	public static void ValidateForProvider(TranIsolationLevel level, DbProvider provider)
	{
		if (provider == DbProvider.PostgreSql && level == TranIsolationLevel.Snapshot)
		{
			throw new NotSupportedTransactionIsolationLevelException(level);
		}

		if (provider == DbProvider.PostgreSql
			&& level is not (
				TranIsolationLevel.ReadCommitted
				or TranIsolationLevel.RepeatableRead
				or TranIsolationLevel.Serializable))
		{
			throw new NotSupportedTransactionIsolationLevelException(level);
		}
	}
}
