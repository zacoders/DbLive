
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationsDeployerTests
{
	static Migration NewMigration(int version) =>
		new()
		{
			Version = version,
			Items = []
		};

	[Fact]
	public void GetMigrationsToApply()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(3),
				NewMigration(4)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);

		mockSet.DbLiveDA.GetCurrentMigrationVersion()
			.Returns(2);

		var migrations = deploy.GetMigrationsToApply(false, DeployParameters.Default).ToArray();

		Assert.Equal(2, migrations.Length);
		Assert.Equal(3, migrations[0].Version);
		Assert.Equal(4, migrations[1].Version);
	}


	[Fact]
	public void GetMigrationsToApply_MaxVersionToDeploy_Specified()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(3),
				NewMigration(4)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);

		mockSet.DbLiveDA.GetCurrentMigrationVersion()
			.Returns(2);

		var deployParams = DeployParameters.Default;
		deployParams.MaxVersionToDeploy = 3;

		var migrations = deploy.GetMigrationsToApply(false, deployParams).ToArray();

		Assert.Single(migrations);

		Assert.Equal(3, migrations[0].Version);
	}

	[Fact]
	public void GetMigrationsToApply_SelfDeploy_DbLiveIsNotInstalled()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(false);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var migrations = deploy.GetMigrationsToApply(true, DeployParameters.Default).ToArray();

		Assert.Equal(4, migrations.Length);
	}

	[Fact]
	public void GetMigrationsToApply_SelfDeploy_DbLiveInstalled()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var migrations = deploy.GetMigrationsToApply(true, DeployParameters.Default).ToArray();

		Assert.Equal(3, migrations.Length);

		Assert.Equal(2, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
		Assert.Equal(3, migrations[2].Version);
	}


	[Fact]
	public void GetMigrationsToApply_SelfDeploy_DbLiveInstalled_MaxVersionToDeploySpecified()
	{
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var deployParams = DeployParameters.Default;
		deployParams.MaxVersionToDeploy = 2;

		var migrations = deploy.GetMigrationsToApply(true, deployParams).ToArray();

		Assert.Equal(2, migrations.Length);
		Assert.Equal(2, migrations[0].Version);
		Assert.Equal(2, migrations[1].Version);
	}


	[Fact]
	public void DeployMigrations()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var deployParams = DeployParameters.Default;
		deployParams.MaxVersionToDeploy = 2;

		// Act
		deploy.DeployMigrations(false, deployParams);

		// Assert
		mockSet.MigrationVersionDeployer.Received(3)
			.DeployMigration(Arg.Is(false), Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}


	[Fact]
	public void DeployMigrations_Empty_Migrations_Folder()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations().Returns([]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var deployParams = DeployParameters.Default;
		deployParams.MaxVersionToDeploy = 2;

		// Act
		deploy.DeployMigrations(false, deployParams);

		// Assert
		mockSet.Logger.Received(1).Information("No migrations to apply.");
		mockSet.MigrationVersionDeployer.DidNotReceive()
			.DeployMigration(Arg.Is(false), Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}


	[Fact]
	public void DeployMigrations_DoNotDeployMigrations()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrations()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var deployParams = DeployParameters.Default;
		deployParams.DeployMigrations = false;

		// Act
		deploy.DeployMigrations(false, deployParams);

		// Assert
		mockSet.MigrationVersionDeployer.DidNotReceive()
			.DeployMigration(Arg.Any<bool>(), Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}
}