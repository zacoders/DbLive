namespace DbLive.Tests.Deployers.Migrations;

public class DowngradeDeployerTests
{
	[Fact]
	public async Task Deploy_downgrade_allowed_executes_undo()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		var appliedMigration1 = new MigrationItemDto
		{
			Version = 1,
			ItemType = MigrationItemType.Migration,
			Name = "migration-1",
			RelativePath = "db/migrations/001.migration.sql",
			ContentHash = 111111,
			Status = MigrationItemStatus.Applied
		};
		
		var appliedMigration2 = new MigrationItemDto
		{
			Version = 2,
			ItemType = MigrationItemType.Migration,
			Name = "migration-2",
			RelativePath = "db/migrations/002.migration.sql",
			ContentHash = 222222,
			Status = MigrationItemStatus.Applied
		};
		
		var appliedMigration3 = new MigrationItemDto
		{
			Version = 3,
			ItemType = MigrationItemType.Migration,
			Name = "migration-3",
			RelativePath = "db/migrations/003.migration.sql",
			ContentHash = 333333,
			Status = MigrationItemStatus.Applied
		};

		var undo3 = new MigrationItemDto
		{
			Version = 3,
			ItemType = MigrationItemType.Undo,
			Name = "undo-3",
			RelativePath = "db/migrations/003.undo.sql",
			ContentHash = 33333333,
			Status = MigrationItemStatus.None
		};

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([appliedMigration1, appliedMigration2, appliedMigration3, undo3]);

		mockSet.DbLiveDA
			.GetMigrationContentAsync(3, MigrationItemType.Undo)
			.Returns("-- undo sql");

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		await deployer.DeployAsync(parameters);

		// Assert
		await mockSet.TransactionRunner.Received(1).ExecuteWithinTransactionAsync(
			true,
			TranIsolationLevel.ReadCommitted,
			Arg.Any<TimeSpan>(),
			Arg.Any<Func<Task>>()
		);

		await mockSet.MigrationItemDeployer.Received(1).DeployAsync(
			3,
			Arg.Is<MigrationItem>(m =>
				m.MigrationItemType == MigrationItemType.Undo &&
				m.FileData.Content == "-- undo sql"
			)
		);
	}


	[Fact]
	public async Task Deploy_downgrade_should_not_execute_since_version3_does_not_applied()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		var appliedMigration1 = new MigrationItemDto
		{
			Version = 1,
			ItemType = MigrationItemType.Migration,
			Name = "migration-1",
			RelativePath = "db/migrations/001.migration.sql",
			ContentHash = 111111,
			Status = MigrationItemStatus.Applied
		};

		var appliedMigration2 = new MigrationItemDto
		{
			Version = 2,
			ItemType = MigrationItemType.Migration,
			Name = "migration-2",
			RelativePath = "db/migrations/002.migration.sql",
			ContentHash = 222222,
			Status = MigrationItemStatus.Applied
		};

		var appliedMigration3 = new MigrationItemDto
		{
			Version = 3,
			ItemType = MigrationItemType.Migration,
			Name = "migration-3",
			RelativePath = "db/migrations/003.migration.sql",
			ContentHash = 333333,
			Status = MigrationItemStatus.None
		};

		var undo3 = new MigrationItemDto
		{
			Version = 3,
			ItemType = MigrationItemType.Undo,
			Name = "undo-3",
			RelativePath = "db/migrations/003.undo.sql",
			ContentHash = 33333333,
			Status = MigrationItemStatus.None
		};

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([appliedMigration1, appliedMigration2, appliedMigration3, undo3]);

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		await deployer.DeployAsync(parameters);

		// Assert
		await mockSet.TransactionRunner.DidNotReceive().ExecuteWithinTransactionAsync(
			Arg.Any<bool>(),
			Arg.Any<TranIsolationLevel>(),
			Arg.Any<TimeSpan>(),
			Arg.Any<Func<Task>>()
		);

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>());		
	}

	[Fact]
	public async Task Deploy_no_applied_versions_outside_project_does_nothing()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		mockSet.DbLiveDA.GetMigrationsAsync().Returns(
			[
				new MigrationItemDto
				{
					Version = 1,
					ItemType = MigrationItemType.Migration,
					Name = "migration-1",
					RelativePath = "db/migrations/001.migration.sql",
					ContentHash = 123456,
					Status = MigrationItemStatus.Applied
				},
				new MigrationItemDto
				{
					Version = 2,
					ItemType = MigrationItemType.Migration,
					Name = "migration-2",
					RelativePath = "db/migrations/002.migration.sql",
					ContentHash = 123456,
					Status = MigrationItemStatus.Applied
				}
			]
		);

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		await deployer.DeployAsync(parameters);

		// Assert
		await mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransactionAsync(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Func<Task>>()
			);

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Deploy_downgrade_detected_but_not_allowed_throws()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		mockSet.DbLiveDA.GetMigrationsAsync().Returns(
			[
				new MigrationItemDto
				{
					Version = 3,
					ItemType = MigrationItemType.Migration,
					Name = "migration-3",
					RelativePath = "db/migrations/003.migration.sql",
					ContentHash = 123456,
					Status = MigrationItemStatus.Applied
				}
			]
		);

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = false
		};

		// Act
		Task act() => deployer.DeployAsync(parameters);

		// Assert
		await Assert.ThrowsAsync<DowngradeNotAllowedException>(act);

		await mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransactionAsync(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Func<Task>>()
			);

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Deploy_missing_undo_scripts_throws_downgrade_impossible()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		// undo for version 3 is missing
		mockSet.DbLiveDA.GetMigrationsAsync().Returns(
			[
				new MigrationItemDto
				{
					Version = 3,
					ItemType = MigrationItemType.Migration,
					Name = "migration-3",
					RelativePath = "db/migrations/003.migration.sql",
					Status = MigrationItemStatus.Applied,
					ContentHash = 123456
				},
				new MigrationItemDto
				{
					Version = 4,
					ItemType = MigrationItemType.Migration,
					Name = "migration-4",
					RelativePath = "db/migrations/004.migration.sql",
					Status = MigrationItemStatus.Applied,
					ContentHash = 123456
				},
				new MigrationItemDto
				{
					Version = 4,
					ItemType = MigrationItemType.Undo,
					Name = "undo-4",
					RelativePath = "db/migrations/004.undo.sql",
					Status = MigrationItemStatus.None,
					ContentHash = 123456
				}
			]
		);

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		Task act() => deployer.DeployAsync(parameters);

		// Assert
		DowngradeImpossibleException ex = await Assert.ThrowsAsync<DowngradeImpossibleException>(act);
		Assert.Contains("Cannot perform downgrade due to missing undo scripts.", ex.Message);

		await mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransactionAsync(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Func<Task>>()
			);

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Deploy_undo_content_missing_throws_downgrade_impossible_exception()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		var undoDto = new MigrationItemDto
		{
			Version = 3,
			ItemType = MigrationItemType.Undo,
			Name = "undo-3",
			RelativePath = "db/migrations/003.undo.sql",
			Status = MigrationItemStatus.None,
			ContentHash = 123456
		};

		mockSet.DbLiveDA.GetMigrationsAsync().Returns(
			[
				new MigrationItemDto
				{
					Version = 3,
					ItemType = MigrationItemType.Migration,
					Name = "migration-3",
					RelativePath = "db/migrations/003.migration.sql",
					Status = MigrationItemStatus.Applied,
					ContentHash = 123456
				},
				undoDto
			]
		);

		mockSet.DbLiveDA
			.GetMigrationContentAsync(3, MigrationItemType.Undo)
			.Returns((string?)null);

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		Task act() => deployer.DeployAsync(parameters);

		// Assert
		DowngradeImpossibleException ex = await Assert.ThrowsAsync<DowngradeImpossibleException>(act);
		Assert.Contains("Undo content for migration version 3 is missing", ex.Message);

		await mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransactionAsync(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Func<Task>>()
			);

		await mockSet.MigrationItemDeployer.DidNotReceive()
			.DeployAsync(Arg.Any<long>(), Arg.Any<MigrationItem>());
	}

}
