
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationItemDeployerTests
{
	[Fact]
	public void DeployMigrationItem_Migration()
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
		deploy.Deploy(1, migrationItem);


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
	public void DeployMigrationItem_Undo()
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

		MigrationItem undoItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			Name = "some-migration",
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		deploy.Deploy(1, undoItem);


		// Assert
		//mockSet.TransactionRunner.Received()
		//	.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.ReadCommitted), Arg.Is(TimeSpan.FromHours(12)), Arg.Any<Action>());

		mockSet.DbLiveDA.Received()
			.ExecuteNonQuery(
				Arg.Is(undoItem.FileData.Content),
				TranIsolationLevel.ReadCommitted,
				TimeSpan.FromHours(12)
			);

		mockSet.DbLiveDA.Received()
			.UpdateMigrationState(Arg.Any<MigrationItemStateDto>());


		Assert.NotNull(savedDto);
		Assert.Equal(MigrationItemStatus.Applied, savedDto.Status);
		Assert.Equal(MigrationItemType.Undo, savedDto.ItemType);
		Assert.Equal(utcNow2, savedDto.AppliedUtc);
		Assert.Equal(2000, savedDto.ExecutionTimeMs);
	}
}