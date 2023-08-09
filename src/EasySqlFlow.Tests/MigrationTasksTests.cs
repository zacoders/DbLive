namespace EasySqlFlow.Tests;

[TestClass]
public class MigrationTasksTests : TestsBase
{
	[TestMethod]
	public void GetMigrationType()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.Setup(fs => fs.EnumerateFiles(It.IsAny<string>(), It.IsAny<string>()))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\003.test3\migration.sql",
				@"C:\MainTestDB\Migrations\003.test3\data.sql",
				@"C:\MainTestDB\Migrations\003.test3\undo.sql",
				@"C:\MainTestDB\Migrations\003.test3\breaking.sql"
			});

		var sqlProject = new SqlProject(mockSet.FileSystem.Object);

		var migrationTasks = sqlProject.GetMigrationTasks("");

		Assert.AreEqual(4, migrationTasks.Count);
	}
}