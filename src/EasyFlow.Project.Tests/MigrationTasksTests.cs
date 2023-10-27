using EasyFlow.Common;

namespace EasyFlow.Project.Tests;

public class MigrationTasksTests
{
	[Fact]
	public void GetMigrationType()
	{
		MockSet mockSet = new();

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");
		var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), "*.sql", settings.TestFilePattern, true)
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\003.test3\migration.sql",
				@"C:\MainTestDB\Migrations\003.test3\data.sql",
				@"C:\MainTestDB\Migrations\003.test3\undo.sql",
				@"C:\MainTestDB\Migrations\003.test3\breaking.sql"
			});

		var migrationTasks = sqlProject.GetMigrationItems("");

		Assert.Equal(4, migrationTasks.Count);
	}

	[Fact]
	public void GetMigrationType_DuplicateTask()
	{
		MockSet mockSet = new();

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load(@"C:\MainTestDB");
		var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>(), settings.TestFilePattern, true)
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\003.test3\migration.sql",
				@"C:\MainTestDB\Migrations\003.test3\undo.sql",
				@"C:\MainTestDB\Migrations\003.test3\UNDO.sql" //Duplicate task name
			});

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.Returns(args =>
			{
				string path = (string)args[0];
				string rootPath = (string)args[1];
				return new FileData
				{
					Content = "test-content",
					FilePath = path,
					RelativePath = path.GetRelativePath(rootPath)
				};
			});

		Assert.Throws<MigrationTaskExistsException>(() => sqlProject.GetMigrationItems(@"C:\MainTestDB\Migrations"));
	}
}