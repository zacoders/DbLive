namespace EasyFlow.Project.Tests;

[TestClass]
public class MigrationTasksTests
{
	[TestMethod]
	public void GetMigrationType()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>())
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\003.test3\migration.sql",
				@"C:\MainTestDB\Migrations\003.test3\data.sql",
				@"C:\MainTestDB\Migrations\003.test3\undo.sql",
				@"C:\MainTestDB\Migrations\003.test3\breaking.sql"
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");

		var migrationTasks = sqlProject.GetMigrationTasks("");

		Assert.AreEqual(4, migrationTasks.Count);
	}

	[TestMethod]
	[ExpectedException(typeof(MigrationTaskExistsException))]
	public void GetMigrationType_DuplicateTask()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.EnumerateFiles(Arg.Any<string>(), Arg.Any<string>())
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\003.test3\migration.sql",
				@"C:\MainTestDB\Migrations\003.test3\undo.sql",
				@"C:\MainTestDB\Migrations\003.test3\UNDO.sql"
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem);
		sqlProject.Load("");

		_ = sqlProject.GetMigrationTasks("");
	}
}