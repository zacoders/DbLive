namespace EasySqlFlow.Tests;

[TestClass]
public class MigrationsTests : TestsBase
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

		var deploy = new DeploySQL(mockSet.FileSystem.Object, mockSet.EasySqlFlowDA.Object);

		var migrations = deploy.GetMigrations("").ToArray();

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
		var fileSystem = new Mock<IFileSystem>();

		fileSystem.Setup(fs => fs.EnumerateDirectories(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.dup1",
				@"C:\MainTestDB\Migrations\001.dup1"
			});

		var deploy = new DeploySQL(fileSystem.Object);

		var migrations = deploy.GetMigrations("");
	}
}