
using NSubstitute.Core;

namespace EasyFlow.Tests;

[TestClass]
public class DeploySqlTest
{
	[TestMethod]
	public void GetMigrationsToApply()
	{
		var mockSet = new MockSet();

		var deploy = new EasyFlow(mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.EasyFlowDeployer);

		static Migration NewMigration(int version, string name) =>
		 new() { Version = version, Name = name, PathUri = new Uri("c:/"), Tasks = new HashSet<MigrationTask>() };
		
		mockSet.EasyFlowProject.GetProjectMigrations()
			.Returns(new[]
			{
				NewMigration(1, "test1"),
				NewMigration(2, "sameversion-1"),
				NewMigration(2, "sameversion-2"),
				NewMigration(3, "test3")
			});

		mockSet.EasyFlowDA.EasyFlowInstalled(Arg.Any<string>())
			.Returns(true);

		mockSet.EasyFlowDA.GetMigrations(Arg.Any<string>(), Arg.Any<string>())
			.Returns(new[]
			{
				new MigrationDto { MigrationVersion = 1, MigrationName = "test1" },
				new MigrationDto { MigrationVersion = 2, MigrationName = "sameversion-2" }
			});

		var migrations = deploy.GetMigrationsToApply("", "", int.MaxValue).ToArray();

		Assert.AreEqual(2, migrations.Length);

		Assert.AreEqual(2, migrations[0].Version);
		Assert.AreEqual("sameversion-1", migrations[0].Name);

		Assert.AreEqual(3, migrations[1].Version);
		Assert.AreEqual("test3", migrations[1].Name);
	}
}