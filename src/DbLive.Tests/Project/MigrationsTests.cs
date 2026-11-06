

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
			"Migrations/003.migration.settings.json",
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
			"Migrations/002.m.s.test.json",
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
			"Migrations/002.m.s.json",
			"Migrations/002.b.test.sql",
			"Migrations/003.m.sql",
			"Migrations/003.b.sql"
		);

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(3, migrations.Count);

		Dictionary<MigrationItemType, MigrationItem> migrationItems = migrations[1].Items;

		Assert.Equal(3, migrationItems.Count);
		Assert.Equal("Migrations/002.m.first.sql", migrationItems[MigrationItemType.Migration].FileData.RelativePath);
		Assert.Equal("Migrations/002.m.s.json", migrationItems[MigrationItemType.MigrationSettings].FileData.RelativePath);
		Assert.Equal("Migrations/002.b.test.sql", migrationItems[MigrationItemType.Breaking].FileData.RelativePath);
	}

	[Fact]
	public async Task GetMigrationItems_MigrationVersionParseException()
	{
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/no-version-provided.sql",
			"Migrations/001.migration.sql",
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
			.AddFile("Migrations/001.B.Sql", "-- breaking")
			.AddFile("Migrations/002.create-users.Sql", "-- migration 2");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(2, migrations.Count);
		Assert.Equal(3, migrations[0].Items.Count);
		Assert.Equal(TransactionWrapLevel.None, migrations[0].GetItemSettings(MigrationItemType.Migration).TransactionWrapLevel);
		Assert.Equal("Migrations/001.B.Sql", migrations[0].Items[MigrationItemType.Breaking].FileData.RelativePath);
		Assert.Equal("create-users", migrations[1].Items[MigrationItemType.Migration].Name);
	}

	[Fact]
	public async Task GetMigrations_Throws_when_migration_item_is_missing()
	{
		// Arrange
		MockSet mockSet = new();

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.ScriptSource.AddFiles(
			"Migrations/005.breaking.settings.json",
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
			.AddFile("Migrations/004.b.s.json", """{ "Timeout": "00:05:00" }""");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		IReadOnlyList<Migration> migrations = await sqlProject.GetMigrationsAsync();

		// Assert
		Migration migration = Assert.Single(migrations);
		Assert.Equal(4, migration.Items.Count);
		Assert.Equal(TransactionWrapLevel.None, migration.GetItemSettings(MigrationItemType.Migration).TransactionWrapLevel);
		Assert.Equal(TimeSpan.FromMinutes(5), migration.GetItemSettings(MigrationItemType.Breaking).Timeout);
	}

	[Theory]
	[InlineData("Migrations/004.undo.sql")]
	[InlineData("Migrations/004.u.sql")]
	[InlineData("Migrations/004.undo.settings.json")]
	[InlineData("Migrations/Old/004.u.s.json")]
	public async Task GetMigrations_throws_when_project_has_an_undo_file(string undoFile)
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.ScriptSource
			.AddFile("Migrations/004.migration.sql", "create table t1 (id int);")
			.AddFile(undoFile, "drop table t1;");

		DbLiveProject sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		// Act
		Task act() => sqlProject.GetMigrationsAsync();

		// Assert
		UndoNotSupportedException ex = await Assert.ThrowsAsync<UndoNotSupportedException>(act);
		Assert.Equal(undoFile, ex.RelativePath);
	}

	[Theory]
	[InlineData("Migrations/004.breaking.settings.json")]
	[InlineData("Migrations/004.b.s.json")]
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
