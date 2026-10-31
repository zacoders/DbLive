
namespace DbLive.Tests.Project;

public class MigrationFileNameParserTests
{
	[Fact]
	public void Throws_When_Version_Is_Not_Number()
	{
		Assert.Throws<InvalidMigrationVersionException>(() =>
			MigrationFileNameParser.GetMigrationInfo("abc.migration.sql"));
	}

	[Theory]
	[InlineData("001.migration.sql", MigrationItemType.Migration)]
	[InlineData("001.m.sql", MigrationItemType.Migration)]
	[InlineData("001.undo.sql", MigrationItemType.Undo)]
	[InlineData("001.u.sql", MigrationItemType.Undo)]
	[InlineData("001.breaking.sql", MigrationItemType.Breaking)]
	[InlineData("001.b.sql", MigrationItemType.Breaking)]
	public void Parses_Explicit_Sql_Types_Correctly(string file, MigrationItemType expectedType)
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo(file);

		Assert.Equal(1, result.Version);
		Assert.Equal(expectedType, result.MigrationItemType);
		Assert.Equal(file, result.FilePath);
	}

	[Theory]
	[InlineData("001.migration.settings.json", MigrationItemType.MigrationSettings)]
	[InlineData("001.m.s.json", MigrationItemType.MigrationSettings)]
	[InlineData("001.m.settings.json", MigrationItemType.MigrationSettings)]
	[InlineData("001.migration.s.json", MigrationItemType.MigrationSettings)]
	[InlineData("001.breaking.settings.json", MigrationItemType.BreakingSettings)]
	[InlineData("001.b.s.json", MigrationItemType.BreakingSettings)]
	[InlineData("001.undo.settings.json", MigrationItemType.UndoSettings)]
	[InlineData("001.u.s.json", MigrationItemType.UndoSettings)]
	[InlineData("001.Undo.Settings.json", MigrationItemType.UndoSettings)]
	public void Parses_Item_Settings_Json_Correctly(string file, MigrationItemType expectedType)
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo(file);

		Assert.Equal(expectedType, result.MigrationItemType);
		Assert.Equal(1, result.Version);
		Assert.Equal(file, result.FilePath);
	}

	[Theory]
	[InlineData("001.settings.json")]
	[InlineData("001.s.json")]
	[InlineData("001.settings.online-index.json")]
	public void Throws_When_Old_Version_Settings_File_Name(string file)
	{
		ObsoleteMigrationSettingsFileException ex = Assert.Throws<ObsoleteMigrationSettingsFileException>(() =>
			MigrationFileNameParser.GetMigrationInfo(file));

		Assert.Contains("001.migration.settings.json", ex.Message);
	}

	[Theory]
	[InlineData("001.migration.json")]
	[InlineData("001.undo.json")]
	[InlineData("001.breaking.json")]
	[InlineData("001.m.json")]
	[InlineData("001.u.json")]
	[InlineData("001.b.json")]
	[InlineData("001.m.config.json")]
	public void Throws_When_Json_Is_Not_Item_Settings(string file)
	{
		Assert.Throws<UnknownMigrationSettingsException>(() =>
			MigrationFileNameParser.GetMigrationInfo(file));
	}

	[Theory]
	[InlineData("001.migration.txt")]
	[InlineData("001.undo.txt")]
	[InlineData("001.b.txt")]
	public void Throws_When_Sql_Type_Is_Not_Sql_Extension(string file)
	{
		Assert.Throws<InvalidMigrationItemTypeException>(() =>
			MigrationFileNameParser.GetMigrationInfo(file));
	}

	[Theory]
	[InlineData("005.settings.sql", "settings")]
	[InlineData("005.m.settings.sql", "settings")]
	public void Settings_Word_In_Sql_File_Is_A_Name(string file, string expectedName)
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo(file);

		Assert.Equal(MigrationItemType.Migration, result.MigrationItemType);
		Assert.Equal(expectedName, result.Name);
	}

	[Fact]
	public void Unknown_Type_With_Sql_Defaults_To_Migration()
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo("003.some-random.sql");

		Assert.Equal(MigrationItemType.Migration, result.MigrationItemType);
		Assert.Equal("some-random", result.Name);
		Assert.Equal(3, result.Version);
	}

	[Fact]
	public void Unknown_Type_With_Json_Throws_Settings_Exception()
	{
		Assert.Throws<UnknownMigrationSettingsException>(() =>
			MigrationFileNameParser.GetMigrationInfo("004.custom.json"));
	}

	[Fact]
	public void Item_Settings_Name_Is_The_Fourth_Part()
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo("004.b.s.drop-column.json");

		Assert.Equal(MigrationItemType.BreakingSettings, result.MigrationItemType);
		Assert.Equal("drop-column", result.Name);
	}

	[Fact]
	public void Parses_Timestamp_Version()
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo("20250621143000.migration.sql");

		Assert.Equal(20250621143000L, result.Version);
		Assert.Equal(MigrationItemType.Migration, result.MigrationItemType);
	}

	[Fact]
	public void Throws_When_Extension_Is_Unknown()
	{
		Assert.Throws<UnknownMigrationItemTypeException>(() =>
			MigrationFileNameParser.GetMigrationInfo("001.whatever.txt"));
	}

	[Fact]
	public void Parses_Migration_Name_When_Present()
	{
		MigrationItemInfo result = MigrationFileNameParser.GetMigrationInfo("010.migration.create-users.sql");

		Assert.Equal("create-users", result.Name);
		Assert.Equal(10, result.Version);
	}
}
