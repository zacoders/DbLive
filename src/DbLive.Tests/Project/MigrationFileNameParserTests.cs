
namespace DbLive.Tests.Project;

public class MigrationFileNameParserTests
{
	[Fact]
	public void Throws_When_Version_Is_Not_Number()
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();

		Assert.Throws<InvalidMigrationVersionException>(() =>
			migrationFileNameParser.GetMigrationInfo("abc.migration.sql", validateVersion: false));
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
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		MigrationItemInfo result = migrationFileNameParser.GetMigrationInfo(file, validateVersion: false);

		Assert.Equal(1, result.Version);
		Assert.Equal(expectedType, result.MigrationItemType);
		Assert.Equal(file, result.FilePath);
	}

	[Theory]
	[InlineData("001.settings.json")]
	[InlineData("001.s.json")]
	public void Parses_Settings_Json_Correctly(string file)
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		MigrationItemInfo result = migrationFileNameParser.GetMigrationInfo(file, validateVersion: false);

		Assert.Equal(MigrationItemType.Settings, result.MigrationItemType);
		Assert.Equal(1, result.Version);
	}

	[Theory]
	[InlineData("001.settings.sql")]
	[InlineData("001.s.sql")]
	public void Throws_When_Settings_Is_Not_Json(string file)
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		Assert.Throws<InvalidMigrationItemTypeException>(() =>
			migrationFileNameParser.GetMigrationInfo(file, validateVersion: false));
	}

	[Theory]
	[InlineData("001.migration.json")]
	[InlineData("001.undo.json")]
	[InlineData("001.breaking.json")]
	[InlineData("001.m.json")]
	[InlineData("001.u.json")]
	[InlineData("001.b.json")]
	public void Throws_When_Sql_Type_Is_Not_Sql_Extension(string file)
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		Assert.Throws<InvalidMigrationItemTypeException>(() =>
			migrationFileNameParser.GetMigrationInfo(file, validateVersion: false));
	}

	[Fact]
	public void Unknown_Type_With_Sql_Defaults_To_Migration()
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		MigrationItemInfo result = migrationFileNameParser.GetMigrationInfo("003.some-random.sql", validateVersion: false);

		Assert.Equal(MigrationItemType.Migration, result.MigrationItemType);
		Assert.Equal("some-random", result.Name);
		Assert.Equal(3, result.Version);
	}

	[Fact]
	public void Unknown_Type_With_Json_Throws_Settings_Exception()
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		Assert.Throws<UnknownMigrationSettingsException>(() =>
			migrationFileNameParser.GetMigrationInfo("004.custom.json", validateVersion: false));
	}

	[Fact]
	public void Parses_Timestamp_Version()
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		MigrationItemInfo result = migrationFileNameParser.GetMigrationInfo("20250621143000.migration.sql", validateVersion: false);

		Assert.Equal(20250621143000L, result.Version);
		Assert.Equal(MigrationItemType.Migration, result.MigrationItemType);
	}

	[Fact]
	public void Throws_When_Extension_Is_Unknown()
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		Assert.Throws<UnknownMigrationItemTypeException>(() =>
			migrationFileNameParser.GetMigrationInfo("001.whatever.txt", validateVersion: false));
	}

	[Fact]
	public void Parses_Migration_Name_When_Present()
	{
		MockSet mockSet = new();
		MigrationFileNameParser migrationFileNameParser = mockSet.CreateUsingMocks<MigrationFileNameParser>();
		MigrationItemInfo result = migrationFileNameParser.GetMigrationInfo("010.migration.create-users.sql", validateVersion: false);

		Assert.Equal("create-users", result.Name);
		Assert.Equal(10, result.Version);
	}
}
