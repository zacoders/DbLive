using EasyFlow.Adapter;
using EasyFlow.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class MigrationItemDeployerTests
{
	[Fact]
	public void MarkAsSkipped_MigrationItem()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		MigrationItemDto? savedDto = null;
		mockSet.EasyFlowDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

		DateTime utcNow = DateTime.UtcNow;
		mockSet.TimeProvider.UtcNow().Returns(utcNow);

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/db/migrations/001.demo",
			Items = new List<MigrationItem>().AsReadOnly()
		};

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		deploy.MarkAsSkipped(false, migration, migrationItem);


		// Assert
		mockSet.EasyFlowDA.Received()
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
		mockSet.EasyFlowDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

		DateTime utcNow = DateTime.UtcNow;
		mockSet.TimeProvider.UtcNow().Returns(utcNow);

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/db/migrations/001.demo",
			Items = new List<MigrationItem>().AsReadOnly()
		};

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/undo.sql",
				FilePath = "c:/db/migrations/001.demo/undo.sql"
			}
		};

		// Act
		deploy.MarkAsSkipped(false, migration, migrationItem);


		// Assert
		mockSet.EasyFlowDA.Received()
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
	public void MarkAsSkipped_MigrationItem_SelfDeploy()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/db/migrations/001.demo",
			Items = new List<MigrationItem>().AsReadOnly()
		};

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		deploy.MarkAsSkipped(true, migration, migrationItem);


		// Assert
		mockSet.EasyFlowDA.DidNotReceive()
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());
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
		mockSet.EasyFlowDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/db/migrations/001.demo",
			Items = new List<MigrationItem>().AsReadOnly()
		};

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		deploy.DeployMigrationItem(false, migration, migrationItem);


		// Assert
		mockSet.TransactionRunner.Received()
			.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.Serializable), Arg.Is(TimeSpan.FromHours(12)), Arg.Any<Action>());

		mockSet.EasyFlowDA.Received()
			.ExecuteNonQuery(Arg.Is(migrationItem.FileData.Content));

		mockSet.EasyFlowDA.Received()
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());


		Assert.NotNull(savedDto);
		Assert.Equal("some-migration", savedDto.Name);
		Assert.Equal(MigrationItemStatus.Applied, savedDto.Status);
		Assert.Equal("", savedDto.Content);
		Assert.Equal(MigrationItemType.Migration, savedDto.ItemType);
		Assert.Equal(utcNow2, savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
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
		mockSet.EasyFlowDA.SaveMigrationItemState(Arg.Do<MigrationItemDto>(dto => savedDto = dto));

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/db/migrations/001.demo",
			Items = new List<MigrationItem>().AsReadOnly()
		};

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Undo,
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		deploy.DeployMigrationItem(false, migration, migrationItem);


		// Assert
		mockSet.TransactionRunner.Received()
			.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.Serializable), Arg.Is(TimeSpan.FromHours(12)), Arg.Any<Action>());

		mockSet.EasyFlowDA.Received()
			.ExecuteNonQuery(Arg.Is(migrationItem.FileData.Content));

		mockSet.EasyFlowDA.Received()
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());


		Assert.NotNull(savedDto);
		Assert.Equal("some-migration", savedDto.Name);
		Assert.Equal(MigrationItemStatus.Applied, savedDto.Status);
		Assert.Equal(migrationItem.FileData.Content, savedDto.Content);
		Assert.Equal(MigrationItemType.Undo, savedDto.ItemType);
		Assert.Equal(utcNow2, savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
		Assert.Equal(utcNow3, savedDto.CreatedUtc);
		Assert.Equal(2000, savedDto.ExecutionTimeMs);
	}


	[Fact]
	public void DeployMigrationItem_SelfDeploy()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<MigrationItemDeployer>();

		Migration migration = new()
		{
			Version = 1,
			Name = "some-migration",
			FolderPath = "c:/db/migrations/001.demo",
			Items = new List<MigrationItem>().AsReadOnly()
		};

		MigrationItem migrationItem = new()
		{
			MigrationItemType = MigrationItemType.Migration,
			FileData = new FileData
			{
				Content = $"-- some sql migration",
				RelativePath = "db/migrations/001.demo/m.1.sql",
				FilePath = "c:/db/migrations/001.demo/m.1.sql"
			}
		};

		// Act
		deploy.DeployMigrationItem(true, migration, migrationItem);


		// Assert
		mockSet.TransactionRunner.Received()
			.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.Serializable), Arg.Is(TimeSpan.FromHours(12)), Arg.Any<Action>());

		mockSet.EasyFlowDA.Received()
			.ExecuteNonQuery(Arg.Is(migrationItem.FileData.Content));

		mockSet.EasyFlowDA.DidNotReceive()
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());
	}
}