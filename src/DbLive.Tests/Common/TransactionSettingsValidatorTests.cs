namespace DbLive.Tests.Common;

public class TransactionSettingsValidatorTests
{
	[Fact]
	public async Task ValidateAsync_AllowsSupportedMsSqlLevels()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.MsSql);

		TransactionSettingsValidator validator = mockSet.CreateUsingMocks<TransactionSettingsValidator>();

		await validator.ValidateAsync(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.Deployment,
			TransactionIsolationLevel = TranIsolationLevel.Snapshot,
			TestsTransactionIsolationLevel = TranIsolationLevel.ReadCommitted
		});
	}

	[Fact]
	public async Task ValidateAsync_RejectsSnapshotOnPostgreSql()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.PostgreSql);

		TransactionSettingsValidator validator = mockSet.CreateUsingMocks<TransactionSettingsValidator>();

		_ = await Assert.ThrowsAsync<NotSupportedTransactionIsolationLevelException>(
			() => validator.ValidateAsync(new DbLiveSettings
			{
				TransactionIsolationLevel = TranIsolationLevel.Snapshot
			})
		);
	}
}
