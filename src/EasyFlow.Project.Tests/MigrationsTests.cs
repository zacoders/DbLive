namespace EasyFlow.Project.Tests;

[TestClass]
public class MigrationsTests
{
	[TestMethod]
	public void TestReadingOfMigrations()
	{
		var mockSet = new MockSet();

		mockSet.FileSystem.Setup(fs => fs.EnumerateDirectories(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.test1",
				@"C:\MainTestDB\Migrations\_Old\002.test2",
				@"C:\MainTestDB\Migrations\004.test4",
				@"C:\MainTestDB\Migrations\003.test3",
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem.Object);

		var migrations = sqlProject.GetProjectMigrations("").ToArray();

		Assert.AreEqual(4, migrations.Count());
		Assert.AreEqual(1, migrations[0].Version);
		Assert.AreEqual(2, migrations[1].Version);
		Assert.AreEqual(3, migrations[2].Version);
		Assert.AreEqual(4, migrations[3].Version);
	}

	[TestMethod]
	[ExpectedException(typeof(MigrationExistsException))]
	public void TestReadingOfMigrations_Duplicate()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.Setup(fs => fs.EnumerateDirectories(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.dup1",
				@"C:\MainTestDB\Migrations\001.dup1"
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem.Object);

		var migrations = sqlProject.GetProjectMigrations("");
	}

	[TestMethod]
	[ExpectedException(typeof(MigrationVersionParseException))]
	public void TestReadingOfMigrations_BadMigrationVersion()
	{
		MockSet mockSet = new();

		mockSet.FileSystem.Setup(fs => fs.EnumerateDirectories(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\bad001version.bad-version-migration"
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem.Object);

		var migrations = sqlProject.GetProjectMigrations("");
	}

	[TestMethod]
	public void GetMigrationsToApply()
	{
		var mockSet = new MockSet();

		mockSet.FileSystem.Setup(fs => fs.EnumerateDirectories(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.test1",
				@"C:\MainTestDB\Migrations\_Old\002.test2",
				@"C:\MainTestDB\Migrations\004.test4",
				@"C:\MainTestDB\Migrations\003.test3",
			});

		var sqlProject = new EasyFlowProject(mockSet.FileSystem.Object);

		var migrations = sqlProject.GetProjectMigrations("").ToArray();

		Assert.AreEqual(4, migrations.Count());
		Assert.AreEqual(1, migrations[0].Version);
		Assert.AreEqual(2, migrations[1].Version);
		Assert.AreEqual(3, migrations[2].Version);
		Assert.AreEqual(4, migrations[3].Version);
	}
}