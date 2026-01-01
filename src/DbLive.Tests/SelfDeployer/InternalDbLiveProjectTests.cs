namespace DbLive.Tests.SelfDeployer;


public class InternalDbLiveProjectTests
{
	[Fact]
	public void GetMigrations_returns_migrations_sorted_by_version()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.InternalProjectPath.Path.Returns(@"C:/Project");

		mockSet.FileSystem.EnumerateFiles(@"C:/Project", "*.sql", true)
			.Returns(
			[
				@"C:/Project/Migrations/002.migration.second.sql",
				@"C:/Project/Migrations/001.migration.first.sql"
			]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), @"C:/Project")
			.Returns(new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo.sql",
				FilePath = "c:/db/migrations/001.demo.sql"
			});

		var project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// act
		var result = project.GetMigrations();

		// assert
		Assert.Equal(2, result.Count);
		Assert.Equal(1, result[0].Version);
		Assert.Equal(2, result[1].Version);
	}

	[Fact]
	public void GetMigrations_creates_single_migration_item_with_expected_properties()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.InternalProjectPath.Path.Returns(@"C:/Project");

		mockSet.FileSystem.EnumerateFiles(@"C:/Project", "*.sql", true)
			.Returns(
			[
				@"C:/Project/Migrations/001.migration.create_table.sql"
			]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), @"C:/Project")
			.Returns(new FileData
			{
				Content = "-- create table",
				RelativePath = "db/migrations/001.migration.create_table.sql",
				FilePath = "C:/Project/Migrations/001.migration.create_table.sql"
			});

		var project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// act
		var result = project.GetMigrations();

		// assert
		Assert.Single(result);

		var migration = result[0];
		Assert.Equal(1, migration.Version);		
		Assert.Equal("create_table", migration.Name);
		Assert.Equal("db/migrations/001.migration.create_table.sql", migration.FileData.RelativePath);
	}

	[Fact]
	public void GetMigrations_reads_file_data_for_each_migration_file()
	{
		// arrange
		MockSet mockSet = new();

		mockSet.InternalProjectPath.Path.Returns(@"C:/Project");

		mockSet.FileSystem.EnumerateFiles(@"C:/Project", "*.sql", true)
			.Returns(
			[
				@"C:/Project/Migrations/001.migration.first.sql",
			@"C:/Project/Migrations/002.migration.second.sql"
			]);

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), @"C:/Project")
			.Returns(new FileData
			{
				Content = "-- sql",
				RelativePath = "db/migrations/test.sql",
				FilePath = "C:/Project/Migrations/test.sql"
			});

		var project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// act
		project.GetMigrations();

		// assert
		mockSet.FileSystem.Received(2)
			.ReadFileData(Arg.Any<string>(), @"C:/Project");
	}

}
