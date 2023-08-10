using EasyFlow.Deploy;

namespace EasyFlow.Tests;

[TestClass]
public class DeploySqlTest : TestsBase
{
	[TestMethod]
	public void GetMigrationsToApply()
	{
		var mockSet = new MockSet();

		var deploy = new EasyFlowDeploy(mockSet.EasyFlowProject.Object, mockSet.EasyFlowDA.Object);

		Migration NewMigration(int version, string name) =>
		 new Migration { Version = version, Name = name, PathUri = new Uri("c:/"), Tasks = new HashSet<MigrationTask>() };

		mockSet.EasyFlowProject.Setup(fs => fs.GetProjectMigrations(It.IsAny<string>()))
			.Returns(new[]
			{
				NewMigration(1, "test1"),
				NewMigration(2, "sameversion-1"),
				NewMigration(2, "sameversion-2"),
				NewMigration(3, "test3")
			});

		mockSet.EasyFlowDA.Setup(fs => fs.GetMigrations(It.IsAny<string>()))
			.Returns(new[]
			{
				new MigrationDto { MigrationVersion = 1, MigrationName = "test1" },
				new MigrationDto { MigrationVersion = 2, MigrationName = "sameversion-2" }
			});

		var migrations = deploy.GetMigrationsToApply("", "").ToArray();

		Assert.AreEqual(2, migrations.Length);

		Assert.AreEqual(2, migrations[0].Version);
		Assert.AreEqual("sameversion-1", migrations[0].Name);

		Assert.AreEqual(3, migrations[1].Version);
		Assert.AreEqual("test3", migrations[1].Name);
	}
}