
namespace DbLive.Tests.Deployers.Migrations;

public class MigrationItemDeployerTests
{
	[Fact]
	public void MarkAsSkipped_MigrationItem()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		MigrationItemDto? savedDto = null;
		mockSet.DbLiveDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

		DateTime utcNow = DateTime.UtcNow;
		mockSet.TimeProvider.UtcNow().Returns(utcNow);

		MigrationItem migrationItem = new()
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
		deploy.MarkAsSkipped(1, migrationItem);


		// Assert
		mockSet.DbLiveDA.Received()
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());


		Assert.NotNull(savedDto);
		Assert.Equal("some-migration", savedDto.Name);
		Assert.Equal(MigrationItemStatus.Skipped, savedDto.Status);
		Assert.Equal("", savedDto.Content);
		Assert.Equal(MigrationItemType.Migration, savedDto.ItemType);
		Assert.Null(savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
		Assert.Equal(utcNow, savedDto.CreatedUtc);
		Assert.Null(savedDto.ExecutionTimeMs);
	}

	[Fact]
	public void MarkAsSkipped_UndoItem()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		MigrationItemDto? savedDto = null;
		mockSet.DbLiveDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

		DateTime utcNow = DateTime.UtcNow;
		mockSet.TimeProvider.UtcNow().Returns(utcNow);

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			Name = "some-migration",
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/undo.sql",
				FilePath = "c:/db/migrations/001.demo/undo.sql"
			}
		};

		// Act
		deploy.MarkAsSkipped(1, migrationItem);


		// Assert
		mockSet.DbLiveDA.Received()
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());


		Assert.NotNull(savedDto);
		Assert.Equal("some-migration", savedDto.Name);
		Assert.Equal(MigrationItemStatus.Skipped, savedDto.Status);
		Assert.Equal(migrationItem.FileData.Content, savedDto.Content);
		Assert.Equal(MigrationItemType.Undo, savedDto.ItemType);
		Assert.Null(savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
		Assert.Equal(utcNow, savedDto.CreatedUtc);
		Assert.Null(savedDto.ExecutionTimeMs);
	}

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

		MigrationItemDto? savedDto = null;
		mockSet.DbLiveDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

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
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());


		Assert.NotNull(savedDto);
		Assert.Equal("some-migration", savedDto.Name);
		Assert.Equal(MigrationItemStatus.Applied, savedDto.Status);
		Assert.Equal("", savedDto.Content);
		Assert.Equal(MigrationItemType.Migration, savedDto.ItemType);
		Assert.Equal(utcNow2, savedDto.AppliedUtc);
		Assert.Equal(1115364988, savedDto.ContentHash);
		Assert.Equal(utcNow3, savedDto.CreatedUtc);
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

		MigrationItemDto? savedDto = null;
		mockSet.DbLiveDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

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
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());


		Assert.NotNull(savedDto);
		Assert.Equal("some-migration", savedDto.Name);
		Assert.Equal(MigrationItemStatus.Applied, savedDto.Status);
		Assert.Equal(undoItem.FileData.Content, savedDto.Content);
		Assert.Equal(MigrationItemType.Undo, savedDto.ItemType);
		Assert.Equal(utcNow2, savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
		Assert.Equal(utcNow3, savedDto.CreatedUtc);
		Assert.Equal(2000, savedDto.ExecutionTimeMs);
	}
}