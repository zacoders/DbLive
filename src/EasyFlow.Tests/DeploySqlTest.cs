namespace EasyFlow.Tests;

public class DeploySqlTest
{
	[Fact]
	public void GetMigrationsToApply()
	{
		var mockSet = new MockSet();

		var deploy = new EasyFlow(mockSet.EasyFlowProject, mockSet.EasyFlowDA, mockSet.EasyFlowPaths);

		static Migration NewMigration(int version, string name) =>
		 new() { Version = version, Name = name, FolderPath = "c:/", Tasks = new HashSet<MigrationItem>() };

		mockSet.EasyFlowProject.GetMigrations()
			.Returns(new[]
			{
				NewMigration(1, "test1"),
				NewMigration(2, "sameversion-1"),
				NewMigration(2, "sameversion-2"),
				NewMigration(3, "test3")
			});

		mockSet.EasyFlowDA.EasyFlowInstalled(Arg.Any<string>())
			.Returns(true);

		mockSet.EasyFlowDA.GetMigrations(Arg.Any<string>())
			.Returns(new[]
			{
				new MigrationDto { Version = 1, Name = "test1" },
				new MigrationDto { Version = 2, Name = "sameversion-2" }
			});

		var migrations = deploy.GetMigrationsToApply(false, "", DeployParameters.Default).ToArray();

		Assert.Equal(2, migrations.Length);

		Assert.Equal(2, migrations[0].Version);
		Assert.Equal("sameversion-1", migrations[0].Name);

		Assert.Equal(3, migrations[1].Version);
		Assert.Equal("test3", migrations[1].Name);
	}
}