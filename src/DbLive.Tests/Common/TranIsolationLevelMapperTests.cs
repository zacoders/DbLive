namespace DbLive.Tests.Common;

public class TranIsolationLevelMapperTests
{
	[Theory]
	[InlineData(TranIsolationLevel.ReadCommitted, IsolationLevel.ReadCommitted)]
	[InlineData(TranIsolationLevel.RepeatableRead, IsolationLevel.RepeatableRead)]
	[InlineData(TranIsolationLevel.Serializable, IsolationLevel.Serializable)]
	[InlineData(TranIsolationLevel.Snapshot, IsolationLevel.Snapshot)]
	public void ToSystemTransaction_MapsSupportedLevels(TranIsolationLevel level, IsolationLevel expected)
	{
		IsolationLevel actual = TranIsolationLevelMapper.ToSystemTransaction(level);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(TranIsolationLevel.ReadCommitted, "read committed")]
	[InlineData(TranIsolationLevel.RepeatableRead, "repeatable read")]
	[InlineData(TranIsolationLevel.Serializable, "serializable")]
	[InlineData(TranIsolationLevel.Snapshot, "snapshot")]
	public void ToSql_MsSql_MapsSupportedLevels(TranIsolationLevel level, string expected)
	{
		string actual = TranIsolationLevelMapper.ToSql(level, DbProvider.MsSql);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(TranIsolationLevel.ReadCommitted, "read committed")]
	[InlineData(TranIsolationLevel.RepeatableRead, "repeatable read")]
	[InlineData(TranIsolationLevel.Serializable, "serializable")]
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
}