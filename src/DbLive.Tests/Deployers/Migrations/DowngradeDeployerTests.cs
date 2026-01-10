namespace DbLive.Tests.Deployers.Migrations;

public class DowngradeDeployerTests
{
	[Fact]
	public async Task Deploy_downgrade_allowed_executes_undo_and_updates_version()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		// database is ahead of project
		mockSet.DbLiveDA.GetCurrentMigrationVersionAsync().Returns(3);

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
			ContentHash = 123456,
			Status = MigrationItemStatus.None
		};

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([undoDto]);

		mockSet.DbLiveDA
			.GetMigrationContentAsync(3, MigrationItemType.Undo)
			.Returns("-- undo sql");

		DateTime completedUtc = DateTime.UtcNow;
		mockSet.TimeProvider.UtcNow().Returns(completedUtc);

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

		await mockSet.DbLiveDA.Received(1)
			.SetCurrentMigrationVersionAsync(2, completedUtc);
	}

	[Fact]
	public async Task Deploy_database_version_not_higher_than_project_version_does_nothing()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveDA.GetCurrentMigrationVersionAsync().Returns(2);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
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
			.DeployAsync(Arg.Any<int>(), Arg.Any<MigrationItem>());

		await mockSet.DbLiveDA.DidNotReceive()
			.SetCurrentMigrationVersionAsync(Arg.Any<int>(), Arg.Any<DateTime>());
	}

	[Fact]
	public async Task Deploy_downgrade_detected_but_not_allowed_throws()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveDA.GetCurrentMigrationVersionAsync().Returns(3);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
			new Migration { Version = 2, Items = [] }
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
			.DeployAsync(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Deploy_missing_undo_scripts_throws_downgrade_impossible()
	{
		// Arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveDA.GetCurrentMigrationVersionAsync().Returns(4);

		mockSet.DbLiveProject.GetMigrationsAsync().Returns(
			[
				new Migration { Version = 1, Items = [] },
			new Migration { Version = 2, Items = [] }
			]
		);

		// only undo for version 4, missing version 3
		mockSet.DbLiveDA.GetMigrationsAsync().Returns(
			[
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
			.DeployAsync(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public async Task Deploy_undo_content_missing_throws_downgrade_impossible_exception()
	{
		// arrange
		MockSet mockSet = new();

		DowngradeDeployer deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		// database ahead of project
		mockSet.DbLiveDA.GetCurrentMigrationVersionAsync().Returns(3);

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

		mockSet.DbLiveDA.GetMigrationsAsync().Returns([undoDto]);

		// critical part: undo script content is missing
		mockSet.DbLiveDA
			.GetMigrationContentAsync(3, MigrationItemType.Undo)
			.Returns((string?)null);

		DeployParameters parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// act
		Task act() => deployer.DeployAsync(parameters);

		// assert
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
			.DeployAsync(Arg.Any<int>(), Arg.Any<MigrationItem>());

		await mockSet.DbLiveDA.DidNotReceive()
			.SetCurrentMigrationVersionAsync(Arg.Any<int>(), Arg.Any<DateTime>());
	}

}