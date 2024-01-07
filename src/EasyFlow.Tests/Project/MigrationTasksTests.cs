namespace EasyFlow.Tests.Project;

public class MigrationTasksTests
{
	[Fact]
	public void GetMigrationType()
	{
		MockSet mockSet = new();

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), settings.TestFilePatterns, true)
			.Returns(
			[
				@"C:\MainTestDB\Migrations\003.test3\migration.sql",
				@"C:\MainTestDB\Migrations\003.test3\undo.sql",
				@"C:\MainTestDB\Migrations\003.test3\breaking.sql"
			]);

		var migrationTasks = sqlProject.GetMigrationItems("");

		Assert.Equal(3, migrationTasks.Count);
	}

	[Fact]
	public void GetMigrationType_SimpleApproach()
	{
		MockSet mockSet = new();

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var settings = sqlProject.GetSettings();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), settings.TestFilePatterns, true)
			.Returns([
				@"C:\MainTestDB\Migrations\002.test\m.sql",
				@"C:\MainTestDB\Migrations\002.test\u.sql",
				@"C:\MainTestDB\Migrations\002.test\b.sql"
			]);

		var migrationTasks = sqlProject.GetMigrationItems("");

		Assert.Equal(3, migrationTasks.Count);
	}


	[Fact]
	public void GetMigrationType_MultipleItemsWithTheSameType()
	{
		MockSet mockSet = new();

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		var settings = sqlProject.GetSettings();

		mockSet.FileSystem.ReadFileData(Arg.Any<string>(), Arg.Any<string>())
			.ReturnsForAnyArgs(call =>
			new FileData
			{
				FilePath = call.Args()[0].ToString()!,
				Content = "",
				RelativePath = ""
			});

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), settings.TestFilePatterns, true)
			.Returns([
				@"C:\DB\Migrations\002.test\m.first.sql",
				@"C:\DB\Migrations\002.test\m.second.sql",
				@"C:\DB\Migrations\002.test\undo.one.sql",
				@"C:\DB\Migrations\002.test\undo.2.sql",
				@"C:\DB\Migrations\002.test\undo.3.sql",
				@"C:\DB\Migrations\002.test\b.01.sql",
				@"C:\DB\Migrations\002.test\b.02.sql",
				@"C:\DB\Migrations\002.test\b.03.sql"
			]);

		var migrationTasks = sqlProject.GetMigrationItems("");

		Assert.Equal(8, migrationTasks.Count);

		// checking order, they will be deploed in this order.
		Assert.Equal(@"C:\DB\Migrations\002.test\b.01.sql", migrationTasks[0].FileData.FilePath);
		Assert.Equal(@"C:\DB\Migrations\002.test\b.02.sql", migrationTasks[1].FileData.FilePath);
		Assert.Equal(@"C:\DB\Migrations\002.test\b.03.sql", migrationTasks[2].FileData.FilePath);
		Assert.Equal(@"C:\DB\Migrations\002.test\undo.one.sql", migrationTasks[7].FileData.FilePath);
	}

	[Fact]
	public void GetMigrationType_EmptySettingsTest()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.FileExists(Arg.Any<string>()).Returns(true);
		mockSet.FileSystem.FileReadAllText(Arg.Any<string>()).Returns("");// empty string

		var sqlProject = new EasyFlowProject(mockSet.ProjectPath, mockSet.FileSystem);

		sqlProject.GetMigrations();
	}
}