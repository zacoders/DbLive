

namespace DbLive.Tests.Project;

public class MigrationsTests
{
	[Fact]
	public async Task TestReadingOfMigrations()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB/";

		mockSet.ProjectPath.Path.Returns(projectPath);
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		mockSet.FileSystem.EnumerateFiles(
			projectPath.CombineWith("Migrations"),
			Arg.Is<IEnumerable<string>>(a => a.Contains("*.sql") && a.Contains("*.json")),
			true
		).Returns([
				@"C:/DB.Migrations/_Old/001.migration.test1.sql",
				@"C:/DB.Migrations/_Old/002.migration.test2.sql",
				@"C:/DB.Migrations/004.migration.test4.sql",
				@"C:/DB.Migrations/003.migration.test3.sql",
			]);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(4, migrations.Count);
		Assert.Equal(1, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
		Assert.Equal(4, migrations[3].Version);
	}

	[Fact]
	public async Task TestReadingOfMigrations_Duplicate()
	{
		MockSet mockSet = new();
		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/_Old/001.migration.dup1.sql",
				@"C:/DB/Migrations/001.migration.dup2.sql"
			]);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<DuplicateMigrationItemException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task TestReadingOfMigrations_BadMigrationVersion()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/bad001version.bad-version-migration"
			]);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<InvalidMigrationVersionException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrationsToApply()
	{
		MockSet mockSet = new();
		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/_Old/001.m.test1.sql",
				@"C:/DB/Migrations/_Old/002.m.test2.sql",
				@"C:/DB/Migrations/003.m.test3.sql",
				@"C:/DB/Migrations/004.m.test4.sql",
			]);

		Migration[] migrations = (await sqlProject.GetMigrationsAsync()).ToArray();

		Assert.Equal(4, migrations.Length);
		Assert.Equal(1, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
		Assert.Equal(4, migrations[3].Version);
	}

	[Fact]
	public async Task GetMigrationType()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns(
			[
				@"C:/DB/Migrations/003.migration.some-note.sql",
				@"C:/DB/Migrations/003.undo.note.sql",
				@"C:/DB/Migrations/003.breaking.sql"
			]);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Single(migrations);
		Assert.Equal(3, migrations[0].Items.Count);
	}

	[Fact]
	public async Task GetMigrationType_SimpleApproach()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		//var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/002.m.test.sql",
				@"C:/DB/Migrations/002.u.test.sql",
				@"C:/DB/Migrations/002.b.test.sql"
			]);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Single(migrations);
		Assert.Equal(3, migrations[0].Items.Count);
	}

	[Fact]
	public async Task GetMigrationType_MultipleMigrations()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "",
				RelativePath = ""
			});

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/001.m.01.sql",
				@"C:/DB/Migrations/001.b.01.sql",
				@"C:/DB/Migrations/002.m.first.sql",
				@"C:/DB/Migrations/002.undo.one.sql",
				@"C:/DB/Migrations/002.b.test.sql",
				@"C:/DB/Migrations/003.m.sql",
				@"C:/DB/Migrations/003.b.sql"
			]);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(3, migrations.Count);

		Dictionary<MigrationItemType, MigrationItem> migrationItems = migrations[1].Items;

		Assert.Equal(3, migrationItems.Count);
		Assert.Equal(@"C:/DB/Migrations/002.m.first.sql", migrationItems[MigrationItemType.Migration].FileData.FilePath);
		Assert.Equal(@"C:/DB/Migrations/002.undo.one.sql", migrationItems[MigrationItemType.Undo].FileData.FilePath);
		Assert.Equal(@"C:/DB/Migrations/002.b.test.sql", migrationItems[MigrationItemType.Breaking].FileData.FilePath);

		//Assert.Equal(@"C:/DB/Migrations/002.test/b.03.sql", migrationItems[2].FileData.FilePath);
		//Assert.Equal(@"C:/DB/Migrations/002.test/undo.one.sql", migrationItems[7].FileData.FilePath);
	}

	[Fact]
	public async Task GetMigrationItems_MigrationVersionParseException()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns(
			[
				@"C:/DB/Migrations/no-version-provided.sql",
				@"C:/DB/Migrations/001.undo.sql",
				@"C:/DB/Migrations/001.breaking.sql"
			]);
		await Assert.ThrowsAsync<InvalidMigrationVersionException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrationItems_UnknownItemType()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns(
			[
				@"C:/DB/Migrations/003.some-unknown-type.txt", // unsupported type (extension)
				@"C:/DB/Migrations/003.undo.sql",
				@"C:/DB/Migrations/003.breaking.sql"
			]);
		await Assert.ThrowsAsync<UnknownMigrationItemTypeException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrations_Throws_when_migration_item_is_missing()
	{
		// Arrange
		MockSet mockSet = new();
		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/005.undo.sql",
			@"C:/DB/Migrations/005.breaking.sql"
			]);

		// Act
		Task act() => sqlProject.GetMigrationsAsync();

		// assert
		await Assert.ThrowsAsync<MigrationItemMustExistsException>(act);
	}

	[Fact]
	public async Task GetMigrations_Returns_empty_when_migrations_folder_is_missing()
	{
		// Arrange
		MockSet mockSet = new();

		string projectPath = @"C:/DB/";
		mockSet.ProjectPath.Path.Returns(projectPath);

		string migrationsPath = projectPath.CombineWith("Migrations");
		mockSet.FileSystem.PathExists(migrationsPath).Returns(false);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		// Assert
		Assert.Empty(migrations);
		mockSet.FileSystem.Received(1).PathExists(migrationsPath);
		mockSet.FileSystem.DidNotReceiveWithAnyArgs().EnumerateFiles(default!, default(IEnumerable<string>)!, default);
	}

	[Fact]
	public async Task GetMigrations_reads_item_settings_files()
	{
		// Arrange
		MockSet mockSet = NewMockSetWithFiles(new Dictionary<string, string>
		{
			[@"C:/DB/Migrations/004.migration.sql"] = "-- migration",
			[@"C:/DB/Migrations/004.migration.settings.json"] = """{ "TransactionWrapLevel": "None" }""",
			[@"C:/DB/Migrations/004.breaking.sql"] = "-- breaking",
			[@"C:/DB/Migrations/004.b.s.json"] = """{ "Timeout": "00:05:00" }""",
			[@"C:/DB/Migrations/004.undo.sql"] = "-- undo",
			[@"C:/DB/Migrations/004.u.settings.json"] = """{ "TransactionWrapLevel": "Migration" }"""
		});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		// Assert
		Migration migration = Assert.Single(migrations);
		Assert.Equal(6, migration.Items.Count);
		Assert.Equal(TransactionWrapLevel.None, migration.GetItemSettings(MigrationItemType.Migration).TransactionWrapLevel);
		Assert.Equal(TimeSpan.FromMinutes(5), migration.GetItemSettings(MigrationItemType.Breaking).Timeout);
		Assert.Equal(TransactionWrapLevel.Migration, migration.GetItemSettings(MigrationItemType.Undo).TransactionWrapLevel);
	}

	[Theory]
	[InlineData(@"C:/DB/Migrations/004.breaking.settings.json")]
	[InlineData(@"C:/DB/Migrations/004.u.s.json")]
	public async Task GetMigrations_throws_when_settings_file_has_no_script(string settingsFile)
	{
		// Arrange
		MockSet mockSet = NewMockSetWithFiles(new Dictionary<string, string>
		{
			[@"C:/DB/Migrations/004.migration.sql"] = "-- migration",
			[settingsFile] = """{ "TransactionWrapLevel": "None" }"""
		});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		Task act() => sqlProject.GetMigrationsAsync();

		// Assert
		await Assert.ThrowsAsync<MigrationSettingsWithoutScriptException>(act);
	}

	[Theory]
	[InlineData("""{ "TransactionWrapLevel": "Deployment" }""", "Deployment")]
	[InlineData("""{ "MigrationTimeout": "01:00:00" }""", "MigrationTimeout")]
	public async Task GetMigrations_throws_when_settings_file_is_invalid(string json, string expectedInMessage)
	{
		// Arrange
		MockSet mockSet = NewMockSetWithFiles(new Dictionary<string, string>
		{
			[@"C:/DB/Migrations/004.migration.sql"] = "-- migration",
			[@"C:/DB/Migrations/004.migration.settings.json"] = json
		});

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		Task act() => sqlProject.GetMigrationsAsync();

		// Assert
		InvalidMigrationItemSettingsException ex = await Assert.ThrowsAsync<InvalidMigrationItemSettingsException>(act);
		Assert.Contains(expectedInMessage, ex.Message);
		Assert.Contains("004.migration.settings.json", ex.Message);
	}

	private static MockSet NewMockSetWithFiles(Dictionary<string, string> files)
	{
		MockSet mockSet = new();
		mockSet.ProjectPath.Path.Returns(@"C:/DB/");
		mockSet.FileSystem.PathExists(Arg.Any<string>()).Returns(true);

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns(files.Keys);

		mockSet.FileSystem.ReadFileDataAsync(Arg.Any<string>(), Arg.Any<string>())
			.Returns(call => new FileData
			{
				FilePath = call.ArgAt<string>(0),
				Content = files[call.ArgAt<string>(0)],
				RelativePath = Path.GetFileName(call.ArgAt<string>(0))
			});

		return mockSet;
	}
}
