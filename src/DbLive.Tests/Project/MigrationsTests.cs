

namespace DbLive.Tests.Project;

public class MigrationsTests
{
	[Fact]
	public async Task TestReadingOfMigrations()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFiles(
			"Migrations/_Old/001.migration.test1.sql",
			"Migrations/_Old/002.migration.test2.sql",
			"Migrations/004.migration.test4.sql",
			"Migrations/003.migration.test3.sql",
			"Code/005.migration.not-a-migration.sql"
		);

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

		mockSet.ScriptSource.AddFiles(
			"Migrations/_Old/001.migration.dup1.sql",
			"Migrations/001.migration.dup2.sql"
		);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<DuplicateMigrationItemException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task TestReadingOfMigrations_BadMigrationVersion()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource.AddFile("Migrations/bad001version.bad-version-migration.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<InvalidMigrationVersionException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task TestReadingOfMigrations_EnforceTimestampVersion_ValidatesWithRelativePath()
	{
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			EnforceTimestampVersion = true
		});

		mockSet.ScriptSource.AddFile("Migrations/20240101000000.migration.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		_ = await sqlProject.GetMigrationsAsync();

		mockSet.MigrationVersionValidator.Received(1).Validate(20240101000000, "Migrations/20240101000000.migration.sql");
	}

	[Fact]
	public async Task GetMigrationsToApply()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/_Old/001.m.test1.sql",
			"Migrations/_Old/002.m.test2.sql",
			"Migrations/003.m.test3.sql",
			"Migrations/004.m.test4.sql"
		);

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

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/003.migration.some-note.sql",
			"Migrations/003.undo.note.sql",
			"Migrations/003.breaking.sql"
		);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Single(migrations);
		Assert.Equal(3, migrations[0].Items.Count);
	}

	[Fact]
	public async Task GetMigrationType_SimpleApproach()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/002.m.test.sql",
			"Migrations/002.u.test.sql",
			"Migrations/002.b.test.sql"
		);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Single(migrations);
		Assert.Equal(3, migrations[0].Items.Count);
	}

	[Fact]
	public async Task GetMigrationType_MultipleMigrations()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/001.m.01.sql",
			"Migrations/001.b.01.sql",
			"Migrations/002.m.first.sql",
			"Migrations/002.undo.one.sql",
			"Migrations/002.b.test.sql",
			"Migrations/003.m.sql",
			"Migrations/003.b.sql"
		);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(3, migrations.Count);

		Dictionary<MigrationItemType, MigrationItem> migrationItems = migrations[1].Items;

		Assert.Equal(3, migrationItems.Count);
		Assert.Equal("Migrations/002.m.first.sql", migrationItems[MigrationItemType.Migration].FileData.RelativePath);
		Assert.Equal("Migrations/002.undo.one.sql", migrationItems[MigrationItemType.Undo].FileData.RelativePath);
		Assert.Equal("Migrations/002.b.test.sql", migrationItems[MigrationItemType.Breaking].FileData.RelativePath);
	}

	[Fact]
	public async Task GetMigrationItems_MigrationVersionParseException()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/no-version-provided.sql",
			"Migrations/001.undo.sql",
			"Migrations/001.breaking.sql"
		);

		await Assert.ThrowsAsync<InvalidMigrationVersionException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrationItems_IgnoresFilesOtherThanSqlAndJson()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/003.some-unknown-type.txt",
			"Migrations/README.md",
			"Migrations/003.migration.sql"
		);

		Migration migration = Assert.Single(await sqlProject.GetMigrationsAsync());
		Assert.Single(migration.Items);
	}

	[Fact]
	public async Task GetMigrations_Reads_Extensions_Ignoring_Case()
	{
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Migrations/001.migration.SQL", "-- migration")
			.AddFile("Migrations/001.m.s.JSON", """{ "TransactionWrapLevel": "None" }""")
			.AddFile("Migrations/001.Undo.Sql", "-- undo")
			.AddFile("Migrations/002.create-users.Sql", "-- migration 2");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(2, migrations.Count);
		Assert.Equal(3, migrations[0].Items.Count);
		Assert.Equal(TransactionWrapLevel.None, migrations[0].GetItemSettings(MigrationItemType.Migration).TransactionWrapLevel);
		Assert.Equal("Migrations/001.Undo.Sql", migrations[0].Items[MigrationItemType.Undo].FileData.RelativePath);
		Assert.Equal("create-users", migrations[1].Items[MigrationItemType.Migration].Name);
	}

	[Fact]
	public async Task GetMigrations_Throws_when_migration_item_is_missing()
	{
		// Arrange
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/005.undo.sql",
			"Migrations/005.breaking.sql"
		);

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

		mockSet.ScriptSource.AddFile("Code/001.migration.sql");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		// Assert
		Assert.Empty(migrations);
	}

	[Fact]
	public async Task GetMigrations_reads_item_settings_files()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Migrations/004.migration.sql", "-- migration")
			.AddFile("Migrations/004.migration.settings.json", """{ "TransactionWrapLevel": "None" }""")
			.AddFile("Migrations/004.breaking.sql", "-- breaking")
			.AddFile("Migrations/004.b.s.json", """{ "Timeout": "00:05:00" }""")
			.AddFile("Migrations/004.undo.sql", "-- undo")
			.AddFile("Migrations/004.u.settings.json", """{ "TransactionWrapLevel": "Migration" }""");

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
	[InlineData("Migrations/004.breaking.settings.json")]
	[InlineData("Migrations/004.u.s.json")]
	public async Task GetMigrations_throws_when_settings_file_has_no_script(string settingsFile)
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Migrations/004.migration.sql", "-- migration")
			.AddFile(settingsFile, """{ "TransactionWrapLevel": "None" }""");

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
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Migrations/004.migration.sql", "-- migration")
			.AddFile("Migrations/004.migration.settings.json", json);

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		Task act() => sqlProject.GetMigrationsAsync();

		// Assert
		InvalidMigrationItemSettingsException ex = await Assert.ThrowsAsync<InvalidMigrationItemSettingsException>(act);
		Assert.Contains(expectedInMessage, ex.Message);
		Assert.Contains("Migrations/004.migration.settings.json", ex.Message);
	}
}
