namespace DbLive.Tests.Deployers.Migrations;

public class MigrationItemDeployerTests
{
	[Fact]
	public async Task DeployMigrationItem_Migration()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDeployer deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime utcNow = DateTime.UtcNow;
		DateTime utcNow2 = utcNow.AddSeconds(2);
		DateTime utcNow3 = utcNow.AddSeconds(3);
		mockSet.TimeProvider.UtcNow().Returns(_ => utcNow, _ => utcNow2, _ => utcNow3);

		MigrationItemStateDto? savedDto = null;
		await mockSet.DbLiveDA.UpdateMigrationStateAsync(Arg.Do<MigrationItemStateDto>(dto => savedDto = dto));

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			Name = "some-migration",
			FileData = new FileData
			{
				Content = $"-- some sql migration content",
				RelativePath = "db/migrations/001.m.some-migration.sql",
				FilePath = "c:/db/migrations/001.m.some-migration.sql"
			}
		};

		// Act
		await deploy.DeployAsync(1, migrationItem, new MigrationItemSettings());


		// Assert
		//mockSet.TransactionRunner.Received()
		//	.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.ReadCommitted), Arg.Is(TimeSpan.FromHours(12)), Arg.Any<Action>());

		await mockSet.DbLiveDA.Received()
			.ExecuteNonQueryAsync(
				Arg.Is(migrationItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		await mockSet.DbLiveDA.Received()
			.UpdateMigrationStateAsync(Arg.Any<MigrationItemStateDto>());


		Assert.NotNull(savedDto);
		Assert.Equal(MigrationItemStatus.Applied, savedDto.Status);
		Assert.Equal(MigrationItemType.Migration, savedDto.ItemType);
		Assert.Equal(utcNow2, savedDto.AppliedUtc);
		Assert.Equal(2000, savedDto.ExecutionTimeMs);
	}


	[Fact]
	public async Task Deploying_Undo__Migration_And_Breaking_Should_Be_Reverted()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDeployer deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime utcNow = DateTime.UtcNow;
		DateTime utcNow2 = utcNow.AddSeconds(2);
		DateTime utcNow3 = utcNow.AddSeconds(3);
		mockSet.TimeProvider.UtcNow().Returns(_ => utcNow, _ => utcNow2, _ => utcNow3);

		mockSet.DbLiveDA.MigrationItemExistsAsync(1, MigrationItemType.Breaking).Returns(true);

		MigrationItem undoItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			Name = "some-undo",
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/u.1.sql",
				FilePath = "c:/db/migrations/001.demo/u.1.sql"
			}
		};

		// Act
		await deploy.DeployAsync(1, undoItem, new MigrationItemSettings());


		// Assert
		await mockSet.DbLiveDA.Received()
			.ExecuteNonQueryAsync(
				Arg.Is(undoItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		await mockSet.DbLiveDA.Received()
			.UpdateMigrationStateAsync(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Undo
					&& dbo.Status == MigrationItemStatus.Applied
					&& dbo.AppliedUtc == utcNow2
					&& dbo.ExecutionTimeMs == 2000
					&& dbo.ErrorMessage == null
				)
			);

		await mockSet.DbLiveDA.Received()
			.UpdateMigrationStateAsync(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Migration
					&& dbo.Status == MigrationItemStatus.Reverted
					&& dbo.AppliedUtc == null
					&& dbo.ExecutionTimeMs == null
				)
			);

		await mockSet.DbLiveDA.Received()
			.UpdateMigrationStateAsync(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Breaking
					&& dbo.Status == MigrationItemStatus.Reverted
					&& dbo.AppliedUtc == null
					&& dbo.ExecutionTimeMs == null
				)
			);
	}



	[Fact]
	public async Task Deploying_Migration__Undo_Should_Be_Reverted()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDeployer deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime utcNow = DateTime.UtcNow;
		DateTime utcNow2 = utcNow.AddSeconds(2);
		DateTime utcNow3 = utcNow.AddSeconds(3);
		mockSet.TimeProvider.UtcNow().Returns(_ => utcNow, _ => utcNow2, _ => utcNow3);

		mockSet.DbLiveDA.MigrationItemExistsAsync(1, MigrationItemType.Undo).Returns(true);

		MigrationItem undoItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			Name = "some-migration",
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		await deploy.DeployAsync(1, undoItem, new MigrationItemSettings());


		// Assert
		await mockSet.DbLiveDA.Received()
			.ExecuteNonQueryAsync(
				Arg.Is(undoItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		await mockSet.DbLiveDA.Received()
			.UpdateMigrationStateAsync(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Migration
					&& dbo.Status == MigrationItemStatus.Applied
					&& dbo.AppliedUtc == utcNow2
					&& dbo.ExecutionTimeMs == 2000
					&& dbo.ErrorMessage == null
				)
			);

		await mockSet.DbLiveDA.Received()
			.UpdateMigrationStateAsync(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Undo
					&& dbo.Status == MigrationItemStatus.None
					&& dbo.AppliedUtc == null
					&& dbo.ExecutionTimeMs == null
				)
			);
	}



	[Fact]
	public async Task Deploy_when_execute_non_query_fails_should_save_failed_state_and_re_throw()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDeployer deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime startUtc = DateTime.UtcNow;
		DateTime endUtc = startUtc.AddSeconds(1);

		mockSet.TimeProvider.UtcNow().Returns(_ => startUtc, _ => endUtc);

		var exception = new DbLiveSqlException("exception message", "some sql error", new Exception());

		mockSet.DbLiveDA
			.When(d => d.ExecuteNonQueryAsync(
				Arg.Any<string>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>()))
			.Do(_ => throw exception);


		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			Name = "broken-migration",
			FileData = new FileData
			{
				Content = "-- broken sql",
				RelativePath = "db/migrations/002.m.broken.sql",
				FilePath = "c:/db/migrations/002.m.broken.sql"
			}
		};

		// Act
		Task act() => deployer.DeployAsync(2, migrationItem, new MigrationItemSettings());

		// Assert
		MigrationDeploymentException ex = await Assert.ThrowsAsync<MigrationDeploymentException>(act);
		Assert.Contains("some sql error", ex.Message);
		Assert.Null(ex.InnerException);
	}

	[Fact]
	public async Task Item_settings_reach_the_transaction_and_the_sql_command()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDeployer deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		MigrationItem migrationItem = NewMigrationItem();

		MigrationItemSettings itemSettings = new()
		{
			TransactionIsolationLevel = TranIsolationLevel.Serializable,
			Timeout = TimeSpan.FromMinutes(5)
		};

		// Act
		await deployer.DeployAsync(1, migrationItem, itemSettings);

		// Assert
		await mockSet.TransactionRunner.Received(1)
			.ExecuteWithinTransactionAsync(
				true,
				TranIsolationLevel.Serializable,
				TimeSpan.FromMinutes(5),
				Arg.Any<Func<Task>>()
			);

		await mockSet.DbLiveDA.Received(1)
			.ExecuteNonQueryAsync(
				migrationItem.FileData.Content,
				TranIsolationLevel.Serializable,
				TimeSpan.FromMinutes(5)
			);
	}

	[Fact]
	public async Task Item_without_transaction_still_gets_its_isolation_level_and_timeout()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDeployer deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		MigrationItem migrationItem = NewMigrationItem();

		MigrationItemSettings itemSettings = new()
		{
			TransactionWrapLevel = TransactionWrapLevel.None,
			TransactionIsolationLevel = TranIsolationLevel.RepeatableRead,
			Timeout = TimeSpan.FromHours(1)
		};

		// Act
		await deployer.DeployAsync(1, migrationItem, itemSettings);

		// Assert
		await mockSet.TransactionRunner.Received(1)
			.ExecuteWithinTransactionAsync(
				false,
				TranIsolationLevel.RepeatableRead,
				TimeSpan.FromHours(1),
				Arg.Any<Func<Task>>()
			);

		await mockSet.DbLiveDA.Received(1)
			.ExecuteNonQueryAsync(
				migrationItem.FileData.Content,
				TranIsolationLevel.RepeatableRead,
				TimeSpan.FromHours(1)
			);
	}

	[Fact]
	public async Task Missing_item_keys_fall_back_to_the_project_settings()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.None,
			TransactionIsolationLevel = TranIsolationLevel.Serializable,
			MigrationTimeout = TimeSpan.FromMinutes(20)
		});

		MigrationItemDeployer deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		// Act
		await deployer.DeployAsync(1, NewMigrationItem(), new MigrationItemSettings());

		// Assert
		await mockSet.TransactionRunner.Received(1)
			.ExecuteWithinTransactionAsync(
				false,
				TranIsolationLevel.Serializable,
				TimeSpan.FromMinutes(20),
				Arg.Any<Func<Task>>()
			);
	}

	[Theory]
	[InlineData(TransactionWrapLevel.Migration, false)]
	[InlineData(TransactionWrapLevel.None, true)]
	public async Task Project_deployment_transaction_runs_the_item_inside_it(TransactionWrapLevel itemTransactionWrapLevel, bool warningExpected)
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.SettingsAccessor.GetProjectSettingsAsync().Returns(new DbLiveSettings
		{
			TransactionWrapLevel = TransactionWrapLevel.Deployment
		});

		MigrationItemDeployer deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		MigrationItem migrationItem = NewMigrationItem();

		// Act
		await deployer.DeployAsync(1, migrationItem, new MigrationItemSettings { TransactionWrapLevel = itemTransactionWrapLevel });

		// Assert
		await mockSet.TransactionRunner.Received(1)
			.ExecuteWithinTransactionAsync(
				false,
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>(),
				Arg.Any<Func<Task>>()
			);

		await mockSet.DbLiveDA.Received(1)
			.ExecuteNonQueryAsync(migrationItem.FileData.Content, Arg.Any<TranIsolationLevel>(), Arg.Any<TimeSpan>());

		mockSet.Logger.Received(warningExpected ? 1 : 0)
			.Warning(Arg.Any<string>(), migrationItem.FileData.RelativePath);
	}

	[Fact]
	public async Task Item_isolation_level_not_supported_by_the_provider_throws_before_running()
	{
		// Arrange
		MockSet mockSet = new();
		mockSet.DbLiveDA.Provider.Returns(DbProvider.PostgreSql);

		MigrationItemDeployer deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		// Act
		Task act() => deployer.DeployAsync(1, NewMigrationItem(), new MigrationItemSettings { TransactionIsolationLevel = TranIsolationLevel.Snapshot });

		// Assert
		await Assert.ThrowsAsync<NotSupportedTransactionIsolationLevelException>(act);

		await mockSet.DbLiveDA.DidNotReceiveWithAnyArgs()
			.ExecuteNonQueryAsync(default!, default, default);
	}

	private static MigrationItem NewMigrationItem()
	{
		return new MigrationItem
		{
			MigrationItemType = MigrationItemType.Migration,
			Name = "some-migration",
			FileData = new FileData
			{
				Content = "-- some sql migration content",
				RelativePath = "db/migrations/001.m.some-migration.sql",
				FilePath = "c:/db/migrations/001.m.some-migration.sql"
			}
		};
	}
}
