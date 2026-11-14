namespace DbLive.Tests.SelfDeployer;


public class InternalDbLiveProjectTests
{
	[Fact]
	public async Task GetMigrations_returns_migrations_sorted_by_version()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.InternalScriptSource.AddFiles(
			"10.migration.tenth.sql",
			"02.migration.second.sql",
			"01.migration.first.sql"
		);

		InternalDbLiveProject project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// Act
		IReadOnlyList<InternalMigration> result = await project.GetMigrationsAsync();

		// Assert
		Assert.Equal([1, 2, 10], result.Select(m => m.Version));
	}

	[Fact]
	public async Task GetMigrations_creates_single_migration_item_with_expected_properties()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.InternalScriptSource.AddFile("01.migration.create_table.sql", "-- create table");

		InternalDbLiveProject project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// Act
		IReadOnlyList<InternalMigration> result = await project.GetMigrationsAsync();

		// Assert
		InternalMigration migration = Assert.Single(result);
		Assert.Equal(1, migration.Version);
		Assert.Equal("create_table", migration.Name);
		Assert.Equal("01.migration.create_table.sql", migration.FileData.RelativePath);
		Assert.Equal("-- create table", migration.FileData.Content);
	}

	[Fact]
	public async Task GetMigrations_reads_its_own_scripts_not_the_project_scripts()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.InternalScriptSource.AddFile("01.migrations.sql", "-- dblive");
		mockSet.ScriptSource.AddFile("Migrations/001.migration.sql", "-- project");

		InternalDbLiveProject project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// Act
		IReadOnlyList<InternalMigration> result = await project.GetMigrationsAsync();

		// Assert
		Assert.Equal("-- dblive", Assert.Single(result).FileData.Content);
	}

	[Fact]
	public async Task GetMigrations_throws_when_no_scripts_are_embedded()
	{
		// Arrange
		MockSet mockSet = new();

		InternalDbLiveProject project = mockSet.CreateUsingMocks<InternalDbLiveProject>();

		// Act
		Task act() => project.GetMigrationsAsync();

		// Assert
		InternalException ex = await Assert.ThrowsAsync<InternalException>(act);
		Assert.Contains("memory", ex.Message);
	}
}
