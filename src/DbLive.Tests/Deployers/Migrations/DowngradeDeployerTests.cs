
namespace DbLive.Tests.Deployers.Migrations;

public class DowngradeDeployerTests
{
	[Fact]
	public void Deploy_downgrade_allowed_executes_undo_and_updates_version()
	{
		// Arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		// database is ahead of project
		mockSet.DbLiveDA.GetCurrentMigrationVersion().Returns(3);

		mockSet.DbLiveProject.GetMigrations().Returns(
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

		mockSet.DbLiveDA.GetMigrations().Returns([undoDto]);

		mockSet.DbLiveDA
			.GetMigrationContent(3, MigrationItemType.Undo)
			.Returns("-- undo sql");

		DateTime completedUtc = DateTime.UtcNow;
		mockSet.TimeProvider.UtcNow().Returns(completedUtc);

		var parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		deployer.Deploy(parameters);

		// Assert
		mockSet.TransactionRunner.Received(1).ExecuteWithinTransaction(
			true,
			TranIsolationLevel.ReadCommitted,
			Arg.Any<TimeSpan>(),
			Arg.Any<Action>()
		);

		mockSet.MigrationItemDeployer.Received(1).Deploy(
			3,
			Arg.Is<MigrationItem>(m =>
				m.MigrationItemType == MigrationItemType.Undo &&
				m.FileData.Content == "-- undo sql"
			)
		);

		mockSet.DbLiveDA.Received(1)
			.SetCurrentMigrationVersion(2, completedUtc);
	}

	[Fact]
	public void Deploy_database_version_not_higher_than_project_version_does_nothing()
	{
		// Arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveDA.GetCurrentMigrationVersion().Returns(2);

		mockSet.DbLiveProject.GetMigrations().Returns(
			[
				new Migration { Version = 1, Items = [] },
				new Migration { Version = 2, Items = [] }
			]
		);

		var parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		deployer.Deploy(parameters);

		// Assert
		mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransaction(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Action>()
			);

		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(Arg.Any<int>(), Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.DidNotReceive()
			.SetCurrentMigrationVersion(Arg.Any<int>(), Arg.Any<DateTime>());
	}

	[Fact]
	public void Deploy_downgrade_detected_but_not_allowed_throws()
	{
		// Arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveDA.GetCurrentMigrationVersion().Returns(3);

		mockSet.DbLiveProject.GetMigrations().Returns(
			[
				new Migration { Version = 1, Items = [] },
			new Migration { Version = 2, Items = [] }
			]
		);

		var parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = false
		};

		// Act
		void act() => deployer.Deploy(parameters);

		// Assert
		Assert.Throws<DowngradeNotAllowedException>(act);

		mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransaction(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Action>()
			);

		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public void Deploy_missing_undo_scripts_throws_downgrade_impossible()
	{
		// Arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		mockSet.DbLiveDA.GetCurrentMigrationVersion().Returns(4);

		mockSet.DbLiveProject.GetMigrations().Returns(
			[
				new Migration { Version = 1, Items = [] },
			new Migration { Version = 2, Items = [] }
			]
		);

		// only undo for version 4, missing version 3
		mockSet.DbLiveDA.GetMigrations().Returns(
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

		var parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// Act
		void act() => deployer.Deploy(parameters);

		// Assert
		var ex = Assert.Throws<DowngradeImpossibleException>(act);
		Assert.Contains("Cannot perform downgrade due to missing undo scripts.", ex.Message);

		mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransaction(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Action>()
			);

		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(Arg.Any<int>(), Arg.Any<MigrationItem>());
	}

	[Fact]
	public void Deploy_undo_content_missing_throws_downgrade_impossible_exception()
	{
		// arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<DowngradeDeployer>();

		// database ahead of project
		mockSet.DbLiveDA.GetCurrentMigrationVersion().Returns(3);

		mockSet.DbLiveProject.GetMigrations().Returns(
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

		mockSet.DbLiveDA.GetMigrations().Returns([undoDto]);

		// critical part: undo script content is missing
		mockSet.DbLiveDA
			.GetMigrationContent(3, MigrationItemType.Undo)
			.Returns((string?)null);

		var parameters = DeployParameters.Default with
		{
			AllowDatabaseDowngrade = true
		};

		// act
		void act() => deployer.Deploy(parameters);

		// assert
		var ex = Assert.Throws<DowngradeImpossibleException>(act);
		Assert.Contains("Undo content for migration version 3 is missing", ex.Message);

		mockSet.TransactionRunner.DidNotReceive()
			.ExecuteWithinTransaction(
				Arg.Any<bool>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Action>()
			);

		mockSet.MigrationItemDeployer.DidNotReceive()
			.Deploy(Arg.Any<int>(), Arg.Any<MigrationItem>());

		mockSet.DbLiveDA.DidNotReceive()
			.SetCurrentMigrationVersion(Arg.Any<int>(), Arg.Any<DateTime>());
	}

}