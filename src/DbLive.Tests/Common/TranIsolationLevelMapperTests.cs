namespace DbLive.Tests.Common;

#pragma warning disable CS0618 // Tests verify deprecated Chaos alias behavior.

public class TranIsolationLevelMapperTests
{
	[Theory]
	[InlineData(TranIsolationLevel.ReadCommitted, IsolationLevel.ReadCommitted)]
	[InlineData(TranIsolationLevel.RepeatableRead, IsolationLevel.RepeatableRead)]
	[InlineData(TranIsolationLevel.Serializable, IsolationLevel.Serializable)]
	[InlineData(TranIsolationLevel.Snapshot, IsolationLevel.Snapshot)]
	[InlineData(TranIsolationLevel.Chaos, IsolationLevel.ReadCommitted)]
	public void ToSystemTransaction_MapsSupportedLevels(TranIsolationLevel level, IsolationLevel expected)
	{
		IsolationLevel actual = TranIsolationLevelMapper.ToSystemTransaction(level);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(TranIsolationLevel.ReadCommitted, "READ COMMITTED")]
	[InlineData(TranIsolationLevel.RepeatableRead, "REPEATABLE READ")]
	[InlineData(TranIsolationLevel.Serializable, "SERIALIZABLE")]
	[InlineData(TranIsolationLevel.Snapshot, "SNAPSHOT")]
	[InlineData(TranIsolationLevel.Chaos, "READ COMMITTED")]
	public void ToSql_MsSql_MapsSupportedLevels(TranIsolationLevel level, string expected)
	{
		string actual = TranIsolationLevelMapper.ToSql(level, DbProvider.MsSql);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(TranIsolationLevel.ReadCommitted, "read committed")]
	[InlineData(TranIsolationLevel.RepeatableRead, "repeatable read")]
	[InlineData(TranIsolationLevel.Serializable, "serializable")]
	[InlineData(TranIsolationLevel.Chaos, "read committed")]
	public void ToSql_PostgreSql_MapsSupportedLevels(TranIsolationLevel level, string expected)
	{
		string actual = TranIsolationLevelMapper.ToSql(level, DbProvider.PostgreSql);
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void ToSql_PostgreSql_Snapshot_Throws()
	{
		_ = Assert.Throws<NotSupportedTransactionIsolationLevelException>(
			() => TranIsolationLevelMapper.ToSql(TranIsolationLevel.Snapshot, DbProvider.PostgreSql)
		);
	}

	[Fact]
	public void ValidateForProvider_PostgreSql_Snapshot_Throws()
	{
		_ = Assert.Throws<NotSupportedTransactionIsolationLevelException>(
			() => TranIsolationLevelMapper.ValidateForProvider(TranIsolationLevel.Snapshot, DbProvider.PostgreSql)
		);
	}

	[Fact]
	public void Normalize_Chaos_AliasesToReadCommitted()
	{
		Assert.Equal(TranIsolationLevel.ReadCommitted, TranIsolationLevelMapper.Normalize(TranIsolationLevel.Chaos));
	}
}

#pragma warning restore CS0618
