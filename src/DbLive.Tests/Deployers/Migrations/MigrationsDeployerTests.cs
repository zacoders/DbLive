
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

		var migrations = deploy.GetMigrationsToApply().ToArray();

		Assert.Equal(2, migrations.Length);
		Assert.Equal(3, migrations[0].Version);
		Assert.Equal(4, migrations[1].Version);
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
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalled().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersion().Returns(1);

		var deployParams = DeployParameters.Default;

		// Act
		deploy.Deploy(deployParams);

		// Assert
		mockSet.MigrationVersionDeployer.Received(3)
			.Deploy(Arg.Any<Migration>(), Arg.Any<DeployParameters>());
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

		// Act
		deploy.Deploy(deployParams);

		// Assert
		mockSet.Logger.Received(1).Information("No migrations to apply.");
		mockSet.MigrationVersionDeployer.DidNotReceive()
			.Deploy(Arg.Any<Migration>(), Arg.Any<DeployParameters>());
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
		deploy.Deploy(deployParams);

		// Assert
		mockSet.MigrationVersionDeployer.DidNotReceive()
			.Deploy(Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}


	[Fact]
	public void DeployInternal_throws_when_migration_item_is_missing()
	{
		// Arrange
		MockSet mockSet = new();
		var deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		var migration = new Migration
		{
			Version = 1,
			Items = []
		};

		// Act
		void act() => deployer.DeployInternal(migration, DeployParameters.Default);

		// Assert
		Assert.Throws<InternalException>(act);
	}

	[Fact]
	public void DeployInternal_migration_undo_migration()
	{
		// Arrange
		MockSet mockSet = new();
		var deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = GetFileData("002.migration.sql", "-- content")
		};

		MigrationItem undoItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = GetFileData("002.undo.sql", "-- content")
		};

		var migration = new Migration
		{
			Version = 2,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = migrationItem,
				[MigrationItemType.Undo] = undoItem
			}
		};

		var parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationUndoMigration
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));

		// Act
		deployer.DeployInternal(migration, parameters);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.Deploy(2, migrationItem);
			mockSet.MigrationItemDeployer.Deploy(2, undoItem);
			mockSet.MigrationItemDeployer.Deploy(2, migrationItem);
		});

		mockSet.DbLiveDA.Received(1)
			.SetCurrentMigrationVersion(2, Arg.Any<DateTime>());
	}

	[Fact]
	public void DeployInternal_migration_breaking_undo_migration()
	{
		// Arrange
		MockSet mockSet = new();
		var deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = GetFileData("003.migration.sql", "-- content")
		};

		MigrationItem breakingItem = new()
		{
			MigrationItemType = MigrationItemType.Breaking,
			FileData = GetFileData("003.breaking.sql", "-- content")
		};

		MigrationItem undoItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			FileData = GetFileData("003.undo.sql", "-- content")
		};

		var migration = new Migration
		{
			Version = 3,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = migrationItem,
				[MigrationItemType.Breaking] = breakingItem,
				[MigrationItemType.Undo] = undoItem
			}
		};

		var parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationBreakingUndoMigration
		};

		// Act
		deployer.DeployInternal(migration, parameters);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.Deploy(3, migrationItem);
			mockSet.MigrationItemDeployer.Deploy(3, breakingItem);
			mockSet.MigrationItemDeployer.Deploy(3, undoItem);
			mockSet.MigrationItemDeployer.Deploy(3, migrationItem);
		});
	}

	[Fact]
	public void DeployInternal_breaking_mode_without_breaking_item()
	{
		// Arrange
		MockSet mockSet = new();
		var deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = GetFileData("004.migration.sql", "-- content")
		};

		MigrationItem undoItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			FileData = GetFileData("004.undo.sql", "-- content")
		};

		var migration = new Migration
		{
			Version = 4,
			Items = new Dictionary<MigrationItemType, MigrationItem>
			{
				[MigrationItemType.Migration] = migrationItem,
				[MigrationItemType.Undo] = undoItem
			}
		};

		var parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationBreakingUndoMigration
		};

		// Act
		deployer.DeployInternal(migration, parameters);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.Deploy(4, migrationItem);
			mockSet.MigrationItemDeployer.Deploy(4, undoItem);
			mockSet.MigrationItemDeployer.Deploy(4, migrationItem);
		});
	}

	[Fact]
	public void DeployInternal_saves_current_migration_version()
	{
		// Arrange
		MockSet mockSet = new();
		var deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		DateTime now = new(2025, 2, 1);
		mockSet.TimeProvider.UtcNow().Returns(now);

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
		deployer.DeployInternal(migration, DeployParameters.Default);

		// Assert
		mockSet.DbLiveDA.Received(1)
			.SetCurrentMigrationVersion(5, now);
	}


	private static FileData GetFileData(string relativePath, string content)
	{
		return new FileData
		{
			Content = content,
			RelativePath = relativePath,
			FilePath = "c:/db/migrations" + relativePath
		};
	}
}