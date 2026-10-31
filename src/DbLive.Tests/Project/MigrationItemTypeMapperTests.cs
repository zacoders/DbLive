namespace DbLive.Tests.Project;

public class MigrationItemTypeMapperTests
{
	[Theory]
	[InlineData(MigrationItemType.Migration, "migration")]
	[InlineData(MigrationItemType.Undo, "undo")]
	[InlineData(MigrationItemType.Breaking, "breaking")]
	[InlineData(MigrationItemType.MigrationSettings, "migration.settings")]
	[InlineData(MigrationItemType.BreakingSettings, "breaking.settings")]
	[InlineData(MigrationItemType.UndoSettings, "undo.settings")]
	public void Maps_item_type_to_db_value_and_back(MigrationItemType itemType, string dbValue)
	{
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
	[InlineData(MigrationItemType.Migration, MigrationItemType.MigrationSettings)]
	[InlineData(MigrationItemType.Breaking, MigrationItemType.BreakingSettings)]
	[InlineData(MigrationItemType.Undo, MigrationItemType.UndoSettings)]
	public void Pairs_script_and_settings_types(MigrationItemType scriptType, MigrationItemType settingsType)
	{
		Assert.Equal(settingsType, MigrationItemTypeMapper.GetSettingsType(scriptType));
		Assert.Equal(scriptType, MigrationItemTypeMapper.GetScriptType(settingsType));
		Assert.True(MigrationItemTypeMapper.IsSettings(settingsType));
		Assert.False(MigrationItemTypeMapper.IsSettings(scriptType));
	}
}
