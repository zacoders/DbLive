
using System.Threading.Tasks;

namespace DbLive.Tests.Deployers.Migrations;

public class MigrationItemDeployerTests
{
	[Fact]
	public async Task DeployMigrationItem_Migration()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime utcNow = DateTime.UtcNow;
		DateTime utcNow2 = utcNow.AddSeconds(2);
		DateTime utcNow3 = utcNow.AddSeconds(3);
		mockSet.TimeProvider.UtcNow().Returns(_ => utcNow, _ => utcNow2, _ => utcNow3);

		MigrationItemStateDto? savedDto = null;
		mockSet.DbLiveDA.UpdateMigrationState(Arg.Do<MigrationItemStateDto>(dto => savedDto = dto));

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
		await deploy.DeployAsync(1, migrationItem);


		// Assert
		//mockSet.TransactionRunner.Received()
		//	.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.ReadCommitted), Arg.Is(TimeSpan.FromHours(12)), Arg.Any<Action>());

		mockSet.DbLiveDA.Received()
			.ExecuteNonQuery(
				Arg.Is(migrationItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Any<MigrationItemStateDto>());


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

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime utcNow = DateTime.UtcNow;
		DateTime utcNow2 = utcNow.AddSeconds(2);
		DateTime utcNow3 = utcNow.AddSeconds(3);
		mockSet.TimeProvider.UtcNow().Returns(_ => utcNow, _ => utcNow2, _ => utcNow3);

		mockSet.DbLiveDA.MigrationItemExists(1, MigrationItemType.Breaking).Returns(true);
		
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
		await deploy.DeployAsync(1, undoItem);


		// Assert
		mockSet.DbLiveDA.Received()
			.ExecuteNonQuery(
				Arg.Is(undoItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Is<MigrationItemStateDto>(
				dbo => 
					dbo.ItemType == MigrationItemType.Undo
					&& dbo.Status == MigrationItemStatus.Applied
					&& dbo.AppliedUtc == utcNow2
					&& dbo.ExecutionTimeMs == 2000
					&& dbo.ErrorMessage == null
				)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Migration
					&& dbo.Status == MigrationItemStatus.Reverted
					&& dbo.AppliedUtc == null
					&& dbo.ExecutionTimeMs == null
				)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Is<MigrationItemStateDto>(
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

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime utcNow = DateTime.UtcNow;
		DateTime utcNow2 = utcNow.AddSeconds(2);
		DateTime utcNow3 = utcNow.AddSeconds(3);
		mockSet.TimeProvider.UtcNow().Returns(_ => utcNow, _ => utcNow2, _ => utcNow3);

		mockSet.DbLiveDA.MigrationItemExists(1, MigrationItemType.Undo).Returns(true);

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
		await deploy.DeployAsync(1, undoItem);


		// Assert
		mockSet.DbLiveDA.Received()
			.ExecuteNonQuery(
				Arg.Is(undoItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Migration
					&& dbo.Status == MigrationItemStatus.Applied
					&& dbo.AppliedUtc == utcNow2
					&& dbo.ExecutionTimeMs == 2000
					&& dbo.ErrorMessage == null
				)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Is<MigrationItemStateDto>(
				dbo =>
					dbo.ItemType == MigrationItemType.Undo
					&& dbo.Status == MigrationItemStatus.None
					&& dbo.AppliedUtc == null
					&& dbo.ExecutionTimeMs == null
				)
			);
	}



	[Fact]
	public async Task Deploy_when_execute_non_query_fails_should_save_failed_state_and_rethrow()
	{
		// Arrange
		MockSet mockSet = new();

		var deployer = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		DateTime startUtc = DateTime.UtcNow;
		DateTime endUtc = startUtc.AddSeconds(1);

		mockSet.TimeProvider.UtcNow().Returns(_ => startUtc, _ => endUtc);

		var exception = new InvalidOperationException("sql error");

		mockSet.DbLiveDA
			.When(d => d.ExecuteNonQuery(
				Arg.Any<string>(),
				Arg.Any<TranIsolationLevel>(),
				Arg.Any<TimeSpan>()))
			.Do(_ => throw exception);

		MigrationItemStateDto? savedDto = null;
		mockSet.DbLiveDA.UpdateMigrationState(
			Arg.Do<MigrationItemStateDto>(dto => savedDto = dto));

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
		Task act() => deployer.DeployAsync(2, migrationItem);

		// Assert
		var ex = await Assert.ThrowsAsync<MigrationDeploymentException>(act);
		Assert.Contains("Migration file deployment error", ex.Message);
		Assert.Same(exception, ex.InnerException);

		mockSet.DbLiveDA.Received(1)
			.UpdateMigrationState(Arg.Any<MigrationItemStateDto>());

		Assert.NotNull(savedDto);
		Assert.Equal(MigrationItemStatus.Failed, savedDto.Status);
		Assert.Equal(MigrationItemType.Migration, savedDto.ItemType);
		Assert.Equal(2, savedDto.Version);
		Assert.Equal(endUtc, savedDto.AppliedUtc);
		Assert.Equal(1000, savedDto.ExecutionTimeMs);
		Assert.Contains("sql error", savedDto.ErrorMessage);
	}

}