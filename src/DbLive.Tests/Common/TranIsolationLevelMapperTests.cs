namespace DbLive.Tests.Common;

public class TranIsolationLevelMapperTests
{
	[Theory]
	[InlineData(nameof(TranIsolationLevel.ReadCommitted), IsolationLevel.ReadCommitted)]
	[InlineData(nameof(TranIsolationLevel.RepeatableRead), IsolationLevel.RepeatableRead)]
	[InlineData(nameof(TranIsolationLevel.Serializable), IsolationLevel.Serializable)]
	[InlineData(nameof(TranIsolationLevel.Snapshot), IsolationLevel.Snapshot)]
	public void ToSystemTransaction_MapsSupportedLevels(string levelName, IsolationLevel expected)
	{
		TranIsolationLevel level = Enum.Parse<TranIsolationLevel>(levelName);
		IsolationLevel actual = TranIsolationLevelMapper.ToSystemTransaction(level);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(nameof(TranIsolationLevel.ReadCommitted), "read committed")]
	[InlineData(nameof(TranIsolationLevel.RepeatableRead), "repeatable read")]
	[InlineData(nameof(TranIsolationLevel.Serializable), "serializable")]
	[InlineData(nameof(TranIsolationLevel.Snapshot), "snapshot")]
	public void ToSql_MsSql_MapsSupportedLevels(string levelName, string expected)
	{
		TranIsolationLevel level = Enum.Parse<TranIsolationLevel>(levelName);
		string actual = TranIsolationLevelMapper.ToSql(level, DbProvider.MsSql);
		Assert.Equal(expected, actual);
	}

	[Theory]
	[InlineData(nameof(TranIsolationLevel.ReadCommitted), "read committed")]
	[InlineData(nameof(TranIsolationLevel.RepeatableRead), "repeatable read")]
	[InlineData(nameof(TranIsolationLevel.Serializable), "serializable")]
	public void ToSql_PostgreSql_MapsSupportedLevels(string levelName, string expected)
	{
		TranIsolationLevel level = Enum.Parse<TranIsolationLevel>(levelName);
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