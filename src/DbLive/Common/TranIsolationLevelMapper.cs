namespace DbLive.Common;

#pragma warning disable CS0618 // TranIsolationLevel.Chaos is intentionally supported as a deprecated alias here.

internal static class TranIsolationLevelMapper
{
	public static TranIsolationLevel Normalize(TranIsolationLevel level)
		=> level == TranIsolationLevel.Chaos ? TranIsolationLevel.ReadCommitted : level;

	public static IsolationLevel ToSystemTransaction(TranIsolationLevel level)
		=> Normalize(level) switch
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

		TranIsolationLevel normalized = Normalize(level);

		return provider switch
		{
			DbProvider.MsSql => normalized switch
			{
				TranIsolationLevel.ReadCommitted => "READ COMMITTED",
				TranIsolationLevel.RepeatableRead => "REPEATABLE READ",
				TranIsolationLevel.Serializable => "SERIALIZABLE",
				TranIsolationLevel.Snapshot => "SNAPSHOT",
				_ => throw new NotSupportedTransactionIsolationLevelException(level)
			},
			DbProvider.PostgreSql => normalized switch
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
				TranIsolationLevel.Chaos,
				TranIsolationLevel.ReadCommitted,
				TranIsolationLevel.RepeatableRead,
				TranIsolationLevel.Serializable,
				TranIsolationLevel.Snapshot
			],
			DbProvider.PostgreSql =>
			[
				TranIsolationLevel.Chaos,
				TranIsolationLevel.ReadCommitted,
				TranIsolationLevel.RepeatableRead,
				TranIsolationLevel.Serializable
			],
			_ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
		};

	public static void ValidateForProvider(TranIsolationLevel level, DbProvider provider)
	{
		if (level == TranIsolationLevel.Chaos)
		{
			return;
		}

		TranIsolationLevel normalized = Normalize(level);

		if (provider == DbProvider.PostgreSql && normalized == TranIsolationLevel.Snapshot)
		{
			throw new NotSupportedTransactionIsolationLevelException(level);
		}

		if (provider == DbProvider.PostgreSql
			&& normalized is not (
				TranIsolationLevel.ReadCommitted
				or TranIsolationLevel.RepeatableRead
				or TranIsolationLevel.Serializable))
		{
			throw new NotSupportedTransactionIsolationLevelException(level);
		}
	}
}

#pragma warning restore CS0618
