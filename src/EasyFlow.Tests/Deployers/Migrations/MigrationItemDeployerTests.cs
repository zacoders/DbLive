using EasyFlow.Adapter;
using EasyFlow.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class MigrationItemDeployerTests
{
	[Fact]
	public void MarkAsSkipped_MigrationItem()
	{
		// Arrange
		var mockSet = new MockSet();

		var deploy = new MigrationItemDeployer(mockSet.Logger, mockSet.EasyFlowDA, mockSet.TimeProvider, mockSet.TransactionRunner);

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
		Assert.Equal("skipped", savedDto.Status);
		Assert.Equal("", savedDto.Content);
		Assert.Equal("migration", savedDto.ItemType);
		Assert.Null(savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
		Assert.Equal(utcNow, savedDto.CreatedUtc);
		Assert.Null(savedDto.ExecutionTimeMs);
	}

	[Fact]
	public void MarkAsSkipped_UndoItem()
	{
		// Arrange
		var mockSet = new MockSet();

		var deploy = new MigrationItemDeployer(mockSet.Logger, mockSet.EasyFlowDA, mockSet.TimeProvider, mockSet.TransactionRunner);

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
		Assert.Equal("skipped", savedDto.Status);
		Assert.Equal(migrationItem.FileData.Content, savedDto.Content);
		Assert.Equal("undo", savedDto.ItemType);
		Assert.Null(savedDto.AppliedUtc);
		Assert.Equal(1715229887, savedDto.ContentHash);
		Assert.Equal(utcNow, savedDto.CreatedUtc);
		Assert.Null(savedDto.ExecutionTimeMs);
	}


	[Fact]
	public void MarkAsSkipped_MigrationItem_SelfDeploy()
	{
		// Arrange
		var mockSet = new MockSet();

		var deploy = new MigrationItemDeployer(mockSet.Logger, mockSet.EasyFlowDA, mockSet.TimeProvider, mockSet.TransactionRunner);

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
	public void DeployMigrationItem()
	{
		// Arrange
		var mockSet = new MockSet();

		var deploy = new MigrationItemDeployer(mockSet.Logger, mockSet.EasyFlowDA, mockSet.TimeProvider, mockSet.TransactionRunner);

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
			.ExecuteWithinTransaction(Arg.Is(false), Arg.Is(TranIsolationLevel.Serializable), Arg.Is(TimeSpan.FromDays(1)), Arg.Any<Action>());
	}
}