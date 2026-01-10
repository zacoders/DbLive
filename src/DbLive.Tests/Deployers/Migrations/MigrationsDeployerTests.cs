using System.Threading.Tasks;

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
	public async Task GetMigrationsToApply()
	{
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(3),
				NewMigration(4)
			]);

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);

		mockSet.DbLiveDA.GetCurrentMigrationVersionAsync()
			.Returns(2);

		Migration[] migrations = (await deploy.GetMigrationsToApplyAsync()).ToArray();

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

		mockSet.DbLiveProject.GetMigrationsAsync()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersionAsync().Returns(1);

		DeployParameters deployParams = DeployParameters.Default;

		// Act
		await deploy.DeployAsync(deployParams);

		// Assert
		await mockSet.MigrationVersionDeployer.Received(3)
			.DeployAsync(Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}


	[Fact]
	public async Task DeployMigrations_Empty_Migrations_Folder()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns([]);

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersionAsync().Returns(1);

		DeployParameters deployParams = DeployParameters.Default;

		// Act
		await deploy.DeployAsync(deployParams);

		// Assert
		mockSet.Logger.Received(1).Information("No migrations to apply.");
		await mockSet.MigrationVersionDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}


	[Fact]
	public async Task DeployMigrations_DoNotDeployMigrations()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationsDeployer deploy = mockSet.CreateUsingMocks<MigrationsDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync()
			.Returns(
			[
				NewMigration(1),
				NewMigration(2),
				NewMigration(2),
				NewMigration(3)
			]);

		mockSet.DbLiveDA.DbLiveInstalledAsync().Returns(true);
		mockSet.DbLiveDA.GetDbLiveVersionAsync().Returns(1);

		DeployParameters deployParams = DeployParameters.Default;
		deployParams.DeployMigrations = false;

		// Act
		await deploy.DeployAsync(deployParams);

		// Assert
		await mockSet.MigrationVersionDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<Migration>(), Arg.Any<DeployParameters>());
	}


	[Fact]
	public async Task DeployInternal_throws_when_migration_item_is_missing()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationVersionDeployer deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

		var migration = new Migration
		{
			Version = 1,
			Items = []
		};

		// Act
		Task act() => deployer.DeployInternalAsync(migration, DeployParameters.Default);

		// Assert
		await Assert.ThrowsAsync<InternalException>(act);
	}

	[Fact]
	public async Task DeployInternal_migration_undo_migration()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationVersionDeployer deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

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

		DeployParameters parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationUndoMigration
		};

		mockSet.TimeProvider.UtcNow().Returns(new DateTime(2025, 1, 1));

		// Act
		await deployer.DeployInternalAsync(migration, parameters);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(2, migrationItem);
			mockSet.MigrationItemDeployer.DeployAsync(2, undoItem);
			mockSet.MigrationItemDeployer.DeployAsync(2, migrationItem);
		});

		await mockSet.DbLiveDA.Received(1)
			.SetCurrentMigrationVersionAsync(2, Arg.Any<DateTime>());
	}

	[Fact]
	public async Task DeployInternal_migration_breaking_undo_migration()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationVersionDeployer deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

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

		DeployParameters parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationBreakingUndoMigration
		};

		// Act
		await deployer.DeployInternalAsync(migration, parameters);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(3, migrationItem);
			mockSet.MigrationItemDeployer.DeployAsync(3, breakingItem);
			mockSet.MigrationItemDeployer.DeployAsync(3, undoItem);
			mockSet.MigrationItemDeployer.DeployAsync(3, migrationItem);
		});
	}

	[Fact]
	public async Task DeployInternal_breaking_mode_without_breaking_item()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationVersionDeployer deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

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

		DeployParameters parameters = DeployParameters.Default with
		{
			UndoTestDeployment = UndoTestMode.MigrationBreakingUndoMigration
		};

		// Act
		await deployer.DeployInternalAsync(migration, parameters);

		// Assert
		Received.InOrder(() =>
		{
			mockSet.MigrationItemDeployer.DeployAsync(4, migrationItem);
			mockSet.MigrationItemDeployer.DeployAsync(4, undoItem);
			mockSet.MigrationItemDeployer.DeployAsync(4, migrationItem);
		});
	}

	[Fact]
	public async Task DeployInternal_saves_current_migration_version()
	{
		// Arrange
		MockSet mockSet = new();
		MigrationVersionDeployer deployer = mockSet.CreateUsingMocks<MigrationVersionDeployer>();

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
		await deployer.DeployInternalAsync(migration, DeployParameters.Default);

		// Assert
		await mockSet.DbLiveDA.Received(1)
			.SetCurrentMigrationVersionAsync(5, now);
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