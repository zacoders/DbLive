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

	[Theory]
	[InlineData(nameof(MigrationItemType.MigrationSettings), "Migrations/005.migration.settings.json")]
	[InlineData(nameof(MigrationItemType.BreakingSettings), "Migrations/005.breaking.settings.json")]
	public void ValidateMigrations_RejectsSnapshotOfAnItemOnPostgreSql(string settingsTypeName, string relativePath)
	{
		// MigrationItemType is internal: a public test method takes the name of the value.
		MigrationItemType settingsType = Enum.Parse<MigrationItemType>(settingsTypeName);

		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.PostgreSql);

		TransactionSettingsValidator validator = mockSet.CreateUsingMocks<TransactionSettingsValidator>();

		Migration migration = NewMigration(5, settingsType, relativePath, """{ "TransactionIsolationLevel": "Snapshot" }""");

		NotSupportedTransactionIsolationLevelException ex = Assert.Throws<NotSupportedTransactionIsolationLevelException>(
			() => validator.ValidateMigrations([NewMigration(4, MigrationItemType.MigrationSettings, "Migrations/004.migration.settings.json", "{}"), migration])
		);

		Assert.Equal($"Transaction isolation level 'Snapshot' in {relativePath} is not supported by PostgreSQL.", ex.Message);
	}

	[Fact]
	public void ValidateMigrations_AllowsSnapshotOfAnItemOnMsSql()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.MsSql);

		TransactionSettingsValidator validator = mockSet.CreateUsingMocks<TransactionSettingsValidator>();

		validator.ValidateMigrations([
			NewMigration(1, MigrationItemType.MigrationSettings, "Migrations/001.migration.settings.json", """{ "TransactionIsolationLevel": "Snapshot" }""")
		]);
	}

	[Fact]
	public void ValidateMigrations_AllowsItemsWithoutALevelOnPostgreSql()
	{
		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.PostgreSql);

		TransactionSettingsValidator validator = mockSet.CreateUsingMocks<TransactionSettingsValidator>();

		validator.ValidateMigrations([
			NewMigration(1, MigrationItemType.MigrationSettings, "Migrations/001.migration.settings.json", """{ "TransactionWrapLevel": "None" }"""),
			NewMigration(2, MigrationItemType.BreakingSettings, "Migrations/002.breaking.settings.json", """{ "TransactionIsolationLevel": "Serializable" }""")
		]);
	}

	/// <summary>
	/// A migration with its script and the settings file of the given type.
	/// </summary>
	private static Migration NewMigration(long version, MigrationItemType settingsType, string settingsPath, string settingsJson)
	{
		MigrationItemType scriptType = MigrationItemTypeMapper.GetScriptType(settingsType);

		return new Migration
		{
			Version = version,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[scriptType] = new()
				{
					MigrationItemType = scriptType,
					FileData = GetFileData(settingsPath.Replace(".settings.json", ".sql"), "select 1;")
				},
				[settingsType] = new()
				{
					MigrationItemType = settingsType,
					FileData = GetFileData(settingsPath, settingsJson)
				}
			}
		};
	}

	private static FileData GetFileData(string relativePath, string content) => new()
	{
		Content = content,
		RelativePath = relativePath
	};
}
