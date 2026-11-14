namespace DbLive.Tests.Deployers.Migrations;

public class MigrationsDeployerTests
{
	static Migration NewMigration(long version) =>
		new()
		{
			Version = version,
			Items = []
		};

	[Fact]
	public async Task GetMigrationsToApply()
	{
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			Migrations = [
				NewMigration(1),
				NewMigration(2),
				NewMigration(3),
				NewMigration(4)
			]
		};

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);

		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync()
			.Returns([1L, 2L]);

		Migration[] migrations = (await deploy.GetMigrationsToApplyAsync(project)).ToArray();

		Assert.Equal(2, migrations.Length);
		Assert.Equal(3, migrations[0].Version);
		Assert.Equal(4, migrations[1].Version);
	}


	[Fact]
	public async Task DeployMigrations()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			Migrations = [
				NewMigration(1),
				NewMigration(2),
				NewMigration(3)
			]
		};

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync().Returns(Array.Empty<long>());

		DeployParameters deployParams = DeployParameters.Default;

		// Act
		await deploy.DeployAsync(project, deployParams);

		// Assert
		await mockSet.MigrationVersionDeployer.Received(3)
			.DeployAsync(Arg.Any<Migration>());
	}


	[Fact]
	public async Task DeployMigrations_Empty_Migrations_Folder()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		ProjectSnapshot project = ProjectSnapshots.Empty;

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetAppliedMigrationVersionsAsync().Returns(Array.Empty<long>());

		DeployParameters deployParams = DeployParameters.Default;

		// Act
		await deploy.DeployAsync(project, deployParams);

		// Assert
		mockSet.Logger.Received(1).Information("No migrations to apply.");
		await mockSet.MigrationVersionDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<Migration>());
	}


	[Fact]
	public async Task DeployMigrations_DoNotDeployMigrations()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		ProjectSnapshot project = ProjectSnapshots.Empty with
		{
			Migrations = [
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]
		};

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetSelfMigrationVersionAsync().Returns(1);

		DeployParameters deployParams = DeployParameters.Default with { DeployMigrations = false };

		// Act
		await deploy.DeployAsync(project, deployParams);

		// Assert
		await mockSet.MigrationVersionDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<Migration>());
	}


	[Fact]
	public async Task DeployVersion_saves_migration_item()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationVersionDeployer deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = GetFileData("005.migration.sql", "-- content")
		};

		var migration = new Migration
		{
			Version = 5,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = migrationItem
			}
		};

		// Act
		await deployer.DeployAsync(migration);

		// Assert
		await mockSet.MigrationItemDeployer.Received(1)
			.DeployAsync(5, migrationItem, new MigrationItemSettings());
	}


	private static FileData GetFileData(string relativePath, string content)
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath
		};
	}
}