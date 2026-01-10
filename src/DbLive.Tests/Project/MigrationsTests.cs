
namespace DbLive.Tests.Project;

public class MigrationsTests
{
	[Fact]
	public async Task TestReadingOfMigrations()
	{
		MockSet mockSet = new();

		string projectPath = @"C:/DB/";

		mockSet.ProjectPath.Path.Returns(projectPath);

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

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		var migrations = await sqlProject.GetMigrationsAsync();

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

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/_Old/001.migration.dup1.sql",
				@"C:/DB/Migrations/001.migration.dup2.sql"
			]);

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<DuplicateMigrationItemException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task TestReadingOfMigrations_BadMigrationVersion()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/bad001version.bad-version-migration"
			]);

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		await Assert.ThrowsAsync<MigrationVersionParseException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrationsToApply()
	{
		MockSet mockSet = new();
		mockSet.ProjectPath.Path.Returns(@"C:/DB/");

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/_Old/001.m.test1.sql",
				@"C:/DB/Migrations/_Old/002.m.test2.sql",
				@"C:/DB/Migrations/003.m.test3.sql",
				@"C:/DB/Migrations/004.m.test4.sql",
			]);

		var migrations = (await sqlProject.GetMigrationsAsync()).ToArray();

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

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns(
			[
				@"C:/DB/Migrations/003.migration.some-note.sql",
				@"C:/DB/Migrations/003.undo.note.sql",
				@"C:/DB/Migrations/003.breaking.sql"
			]);

		var migrations = await sqlProject.GetMigrationsAsync();

		Assert.Single(migrations);
		Assert.Equal(3, migrations[0].Items.Count);
	}

	[Fact]
	public async Task GetMigrationType_SimpleApproach()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		//var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns([
				@"C:/DB/Migrations/002.m.test.sql",
				@"C:/DB/Migrations/002.u.test.sql",
				@"C:/DB/Migrations/002.b.test.sql"
			]);

		var migrations = await sqlProject.GetMigrationsAsync();

		Assert.Single(migrations);
		Assert.Equal(3, migrations[0].Items.Count);
	}

	[Fact]
	public async Task GetMigrationType_MultipleMigrations()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

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

		var migrations = await sqlProject.GetMigrationsAsync();

		Assert.Equal(3, migrations.Count);

		var migrationItems = migrations[1].Items;

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

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), true)
			.Returns(
			[
				@"C:/DB/Migrations/no-version-provided.sql",
				@"C:/DB/Migrations/001.undo.sql",
				@"C:/DB/Migrations/001.breaking.sql"
			]);
		await Assert.ThrowsAsync<MigrationVersionParseException>(sqlProject.GetMigrationsAsync);
	}

	[Fact]
	public async Task GetMigrationItems_UnknownItemType()
	{
		MockSet mockSet = new();

		mockSet.ProjectPath.Path.Returns(@"C:/DB/");

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

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

		var sqlProject = mockSet.CreateUsingMocks<DbLiveProject>();

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
}