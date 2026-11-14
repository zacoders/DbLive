namespace DbLive.Tests.Project;

public class MigrationItemTypeMapperTests
{
	[Theory]
	[InlineData(nameof(MigrationItemType.Migration), "migration")]
	[InlineData(nameof(MigrationItemType.Breaking), "breaking")]
	[InlineData(nameof(MigrationItemType.MigrationSettings), "migration.settings")]
	[InlineData(nameof(MigrationItemType.BreakingSettings), "breaking.settings")]
	public void Maps_item_type_to_db_value_and_back(string itemTypeName, string dbValue)
	{
		// MigrationItemType is internal: a public test method takes the name of the value.
		MigrationItemType itemType = Enum.Parse<MigrationItemType>(itemTypeName);

		Assert.Equal(dbValue, MigrationItemTypeMapper.ToDbValue(itemType));
		Assert.Equal(itemType, MigrationItemTypeMapper.FromDbValue(dbValue));
	}

	[Fact]
	public void Every_item_type_has_a_db_value_that_fits_the_column()
	{
		foreach (MigrationItemType itemType in Enum.GetValues<MigrationItemType>())
		{
			// dblive.migration.item_type is varchar(32)
			Assert.InRange(MigrationItemTypeMapper.ToDbValue(itemType).Length, 1, 32);
		}
	}

	[Fact]
	public void Old_settings_value_is_unknown()
	{
		// renamed to 'migration.settings' by the self-deploy scripts.
		Assert.Throws<ArgumentOutOfRangeException>(() => MigrationItemTypeMapper.FromDbValue("settings"));
	}

	[Theory]
	[InlineData("undo")]
	[InlineData("undo.settings")]
	public void Undo_values_are_unknown(string dbValue)
	{
		// deleted by the self-deploy scripts.
		Assert.Throws<ArgumentOutOfRangeException>(() => MigrationItemTypeMapper.FromDbValue(dbValue));
	}

	[Theory]
	[InlineData(nameof(MigrationItemType.Migration), nameof(MigrationItemType.MigrationSettings))]
	[InlineData(nameof(MigrationItemType.Breaking), nameof(MigrationItemType.BreakingSettings))]
	public void Pairs_script_and_settings_types(string scriptTypeName, string settingsTypeName)
	{
		// MigrationItemType is internal: a public test method takes the name of the value.
		MigrationItemType scriptType = Enum.Parse<MigrationItemType>(scriptTypeName);
		MigrationItemType settingsType = Enum.Parse<MigrationItemType>(settingsTypeName);

		Assert.Equal(settingsType, MigrationItemTypeMapper.GetSettingsType(scriptType));
		Assert.Equal(scriptType, MigrationItemTypeMapper.GetScriptType(settingsType));
		Assert.True(MigrationItemTypeMapper.IsSettings(settingsType));
		Assert.False(MigrationItemTypeMapper.IsSettings(scriptType));
	}
}
