namespace EasyFlow.Project.Tests;

public class MigrationsTests
{
	[Fact]
	public void TestReadingOfMigrations()
	{
		var mockSet = new MockSet();

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string>(), "*.*", SearchOption.AllDirectories)
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.test1",
				@"C:\MainTestDB\Migrations\_Old\002.test2",
				@"C:\MainTestDB\Migrations\004.test4",
				@"C:\MainTestDB\Migrations\003.test3",
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");

		var migrations = sqlProject.GetMigrations().ToArray();

		Assert.Equal(4, migrations.Count());
		Assert.Equal(1, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
		Assert.Equal(4, migrations[3].Version);
	}

	[Fact]
	public void TestReadingOfMigrations_Duplicate()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string>(), "*.*", SearchOption.AllDirectories)
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.dup1",
				@"C:\MainTestDB\Migrations\001.dup1"
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");

		Assert.Throws<MigrationExistsException>(sqlProject.GetMigrations);
	}

	[Fact]
	public void TestReadingOfMigrations_BadMigrationVersion()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string>(), "*.*", SearchOption.AllDirectories)
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\bad001version.bad-version-migration"
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");

		Assert.Throws<MigrationVersionParseException>(sqlProject.GetMigrations);
	}

	[Fact]
	public void GetMigrationsToApply()
	{
		var mockSet = new MockSet();

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string>(), "*.*", SearchOption.AllDirectories)
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.test1",
				@"C:\MainTestDB\Migrations\_Old\002.test2",
				@"C:\MainTestDB\Migrations\004.test4",
				@"C:\MainTestDB\Migrations\003.test3",
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");

		var migrations = sqlProject.GetMigrations().ToArray();

		Assert.Equal(4, migrations.Count());
		Assert.Equal(1, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
		Assert.Equal(4, migrations[3].Version);
	}
}