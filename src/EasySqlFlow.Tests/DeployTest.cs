
[TestClass]
public class DeploySQLTest : TestsBase
{
    [TestMethod]
    public void TestReadingOfMigrations()
    {
		var fileSystem = new Mock<IFileSystem>();

		fileSystem.Setup(fs => fs.EnumerateDirectories(It.IsAny<string>(), "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\MainTestDB\Migrations\_Old\001.test1",
				@"C:\MainTestDB\Migrations\_Old\002.test2",
				@"C:\MainTestDB\Migrations\003.test3",
				@"C:\MainTestDB\Migrations\004.test4",
			});

		var deploy = new DeploySQL(fileSystem.Object);

		var migrations = deploy.GetMigrations("");

		Assert.AreEqual(4, migrations.Count);
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