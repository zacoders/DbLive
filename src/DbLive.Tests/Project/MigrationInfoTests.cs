
namespace DbLive.Tests.Project;

public class MigrationInfoTests
{
	[Fact]
	public void Throws_when_version_is_not_number()
	{
		Assert.Throws<MigrationVersionParseException>(() =>
			DbLiveProject.GetMigrationInfo("abc.migration.sql"));
	}

	[Theory]
	[InlineData("001.migration.sql", MigrationItemType.Migration)]
	[InlineData("001.m.sql", MigrationItemType.Migration)]
	[InlineData("001.undo.sql", MigrationItemType.Undo)]
	[InlineData("001.u.sql", MigrationItemType.Undo)]
	[InlineData("001.breaking.sql", MigrationItemType.Breaking)]
	[InlineData("001.b.sql", MigrationItemType.Breaking)]
	public void Parses_explicit_sql_types_correctly(string file, MigrationItemType expectedType)
	{
		var result = DbLiveProject.GetMigrationInfo(file);

		Assert.Equal(1, result.Version);
		Assert.Equal(expectedType, result.MigrationItemType);
		Assert.Equal(file, result.FilePath);
	}

	[Theory]
	[InlineData("001.settings.json")]
	[InlineData("001.s.json")]
	public void Parses_settings_json_correctly(string file)
	{
		var result = DbLiveProject.GetMigrationInfo(file);

		Assert.Equal(MigrationItemType.Settings, result.MigrationItemType);
		Assert.Equal(1, result.Version);
	}

	[Theory]
	[InlineData("001.settings.sql")]
	[InlineData("001.s.sql")]
	public void Throws_when_settings_is_not_json(string file)
	{
		Assert.Throws<InvalidMigrationItemTypeException>(() => DbLiveProject.GetMigrationInfo(file));
	}

	[Theory]
	[InlineData("001.migration.json")]
	[InlineData("001.undo.json")]
	[InlineData("001.breaking.json")]
	[InlineData("001.m.json")]
	[InlineData("001.u.json")]
	[InlineData("001.b.json")]
	public void Throws_when_sql_type_is_not_sql_extension(string file)
	{
		Assert.Throws<InvalidMigrationItemTypeException>(() => DbLiveProject.GetMigrationInfo(file));
	}

	[Fact]
	public void Unknown_type_with_sql_defaults_to_migration()
	{
		var result = DbLiveProject.GetMigrationInfo("003.some-random.sql");

		Assert.Equal(MigrationItemType.Migration, result.MigrationItemType);
		Assert.Equal(3, result.Version);
	}

	[Fact]
	public void Unknown_type_with_json_defaults_to_settings()
	{
		Assert.Throws<UnknownMigrationSettingsException>(() =>
			DbLiveProject.GetMigrationInfo("004.custom.json")
		);
	}

	[Fact]
	public void Throws_when_extension_is_unknown()
	{
		Assert.Throws<UnknownMigrationItemTypeException>(() =>
			DbLiveProject.GetMigrationInfo("001.whatever.txt"));
	}

	[Fact]
	public void Parses_migration_name_when_present()
	{
		var result = DbLiveProject.GetMigrationInfo("010.migration.create-users.sql");

		Assert.Equal("create-users", result.Name);
		Assert.Equal(10, result.Version);
	}
}
