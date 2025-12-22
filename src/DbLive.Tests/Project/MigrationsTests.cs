using DbLive.Project.Exceptions;

namespace EasyFlow.Tests.Project;

public class MigrationsTests
{
	[Fact]
	public void TestReadingOfMigrations()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string[]>(), "*", SearchOption.TopDirectoryOnly)
			.Returns([
				@"C:\DB\Migrations\_Old\001.test1",
				@"C:\DB\Migrations\_Old\002.test2",
				@"C:\DB\Migrations\_Old", // should be skipped.
				@"C:\DB\Migrations\004.test4",
				@"C:\DB\Migrations\003.test3",
			]);

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		var migrations = sqlProject.GetMigrations().ToArray();

		Assert.Equal(4, migrations.Length);
		Assert.Equal(1, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
		Assert.Equal(4, migrations[3].Version);
	}

	[Fact]
	public void TestReadingOfMigrations_Duplicate()
	{
		MockSet mockSet = new();
		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string[]>(), "*", SearchOption.TopDirectoryOnly)
			.Returns([
				@"C:\DB\Migrations\_Old\001.dup1",
				@"C:\DB\Migrations\001.dup1"
			]);

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		Assert.Throws<MigrationExistsException>(sqlProject.GetMigrations);
	}

	[Fact]
	public void TestReadingOfMigrations_BadMigrationVersion()
	{
		MockSet mockSet = new();

		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string[]>(), "*", SearchOption.TopDirectoryOnly)
			.Returns([
				@"C:\DB\Migrations\bad001version.bad-version-migration"
			]);

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		Assert.Throws<MigrationVersionParseException>(sqlProject.GetMigrations);
	}

	[Fact]
	public void GetMigrationsToApply()
	{
		MockSet mockSet = new();
		mockSet.ProjectPathAccessor.ProjectPath.Returns(@"C:\DB\");

		var sqlProject = mockSet.CreateUsingMocks<EasyFlowProject>();

		mockSet.FileSystem.EnumerateDirectories(Arg.Any<string[]>(), "*", SearchOption.TopDirectoryOnly)
			.Returns([
				@"C:\DB\Migrations\_Old\001.test1",
				@"C:\DB\Migrations\_Old\002.test2",
				@"C:\DB\Migrations\004.test4",
				@"C:\DB\Migrations\003.test3",
			]);

		var migrations = sqlProject.GetMigrations().ToArray();

		Assert.Equal(4, migrations.Length);
		Assert.Equal(1, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
		Assert.Equal(4, migrations[3].Version);
	}
}