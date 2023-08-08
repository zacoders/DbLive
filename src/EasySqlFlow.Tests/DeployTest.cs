using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
public class DeploySQLTest : TestsBase
{
    [TestMethod]
    public void TestReadingOfMigrations()
    {
        string path = @"C:\Data\Code\Personal\EasySqlFlow\src\TestDatabases\MainTestDB";

		var fileSystem = new Mock<IFileSystem>();

		// WOW! No record/replay weirdness?! :)
		fileSystem.Setup(fs => fs.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
			.Returns(new[]
			{
				@"C:\Data\src\TestDatabases\MainTestDB\Migrations\001.test1",
				@"C:\Data\src\TestDatabases\MainTestDB\Migrations\002.test2",
				@"C:\Data\src\TestDatabases\MainTestDB\Migrations\003.test3",
				@"C:\Data\src\TestDatabases\MainTestDB\Migrations\004.test4",
			});

		var deploy = new DeploySQL(fileSystem.Object);

		var migrations = deploy.GetMigrations(path);

		Assert.AreEqual(4, migrations.Count);
	}
}