using EasyFlow.Adapter;
using EasyFlow.Deployers.Migrations;
using EasyFlow.Exceptions;

namespace EasyFlow.Tests.Deployers.Migrations;

public class BreakingChangesDeployerTests
{
	[Fact]
	public void DeployBreakingChanges_SkipBreakingChangesDeployment()
	{
		// Arrange
		MockSet mockSet = new();

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deploy.DeployBreakingChanges(new DeployParameters { DeployBreaking = false });

		// Assert
		mockSet.EasyFlowDA.DidNotReceive().GetNonAppliedBreakingMigrationItems();
	}


	[Fact]
	public void DeployBreakingChanges_NoBreaknigChangesItemssToApply()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowDA.GetNonAppliedBreakingMigrationItems()
			.Returns(new List<MigrationItemDto>().AsReadOnly());

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deploy.DeployBreakingChanges(DeployParameters.Breaking);

		// Assert
		mockSet.EasyFlowProject.DidNotReceive().GetMigrations();
	}


	[Fact]
	public void DeployBreakingChanges()
	{
		// Arrange
		MockSet mockSet = new();

		MigrationItemDto migrationItemDto1 = new()
		{
			Version = 2,
			ContentHash = "-- content 2.breaking".Crc32HashCode(),
			Content = "-- content 2.breaking",
			ItemType = MigrationItemType.Breaking,
			Name = "second-migration",
			Status = MigrationItemStatus.Skipped
		};

		MigrationItemDto migrationItemDto2 = new()
		{
			Version = 3,
			ContentHash = "-- content 3.breaking".Crc32HashCode(),
			Content = "-- content 3.breaking",
			ItemType = MigrationItemType.Breaking,
			Name = "3rd-migration",
			Status = MigrationItemStatus.Skipped
		};

		mockSet.EasyFlowDA.GetNonAppliedBreakingMigrationItems().Returns([migrationItemDto1, migrationItemDto2]);

		Migration migration1 = new()
		{
			FolderPath = "c:/db/migrations/001.some-migration",
			Name = "some-migration",
			Version = 1,
			Items = new List<MigrationItem>
				{
					new() {
						MigrationItemType = MigrationItemType.Migration,
						FileData = GetFileData("m.1.item.sql", "-- content 1.1")
					},
					new() {
						MigrationItemType = MigrationItemType.Breaking,
						FileData = GetFileData("breaking.sql", "-- content 1.breaking")
					},
				}.AsReadOnly()
		};

		Migration migration2 = new()
		{
			FolderPath = "c:/db/migrations/002.second-migration",
			Name = "second-migration",
			Version = 2,
			Items = new List<MigrationItem>
					{
						new() {
							MigrationItemType = MigrationItemType.Migration,
							FileData = GetFileData("m.1.item.sql", "-- content 2.1")
						},
						new() {
							MigrationItemType = MigrationItemType.Breaking,
							FileData = GetFileData("breaking.sql", "-- content 2.breaking")
						},
					}.AsReadOnly()
		};

		Migration migration3 = new()
		{
			FolderPath = "c:/db/migrations/003.second-migration",
			Name = "3rd-migration",
			Version = 3,
			Items = new List<MigrationItem>
					{
						new() {
							MigrationItemType = MigrationItemType.Migration,
							FileData = GetFileData("m.1.item.sql", "-- content 3.1")
						},
						new() {
							MigrationItemType = MigrationItemType.Migration,
							FileData = GetFileData("m.2.item.sql", "-- content 3.2")
						},
						new() {
							MigrationItemType = MigrationItemType.Breaking,
							FileData = GetFileData("breaking.sql", "-- content 3.breaking")
						},
					}.AsReadOnly()
		};

		Migration migration4 = new()
		{
			FolderPath = "c:/db/migrations/004.4th-migration",
			Name = "4th-migration",
			Version = 4,
			Items = new List<MigrationItem>
					{
						new() {
							MigrationItemType = MigrationItemType.Migration,
							FileData = GetFileData("m.1.item.sql", "-- content 4.1")
						},
						new() {
							MigrationItemType = MigrationItemType.Migration,
							FileData = GetFileData("m.2.item.sql", "-- content 4.2")
						},
						new() {
							MigrationItemType = MigrationItemType.Breaking,
							FileData = GetFileData("breaking.sql", "-- content 4.breaking")
						},
					}.AsReadOnly()
		};

		mockSet.EasyFlowProject.GetMigrations().Returns([migration1, migration2, migration3, migration4]);

		IStopWatch mockStopWatch = Substitute.For<IStopWatch>();
		mockStopWatch.ElapsedMilliseconds.Returns(_ => 1555, _ => 2555);
		mockSet.TimeProvider.StartNewStopwatch().Returns(mockStopWatch);

		DateTime appliedUtc1 = new DateTime(2024, 1, 1, 5, 10, 0);
		DateTime appliedUtc2 = new DateTime(2024, 1, 1, 5, 10, 35);
		mockSet.TimeProvider.UtcNow().Returns(
			_ => appliedUtc1,
			_ => appliedUtc2
		);

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deploy.DeployBreakingChanges(DeployParameters.Breaking);

		// Assert
		mockSet.EasyFlowProject.Received().GetMigrations();

		mockSet.MigrationItemDeployer.Received()
			.DeployMigrationItem(Arg.Is(false), Arg.Is(migration2), Arg.Is(migration2.Items[1]));

		mockSet.MigrationItemDeployer.Received()
			.DeployMigrationItem(Arg.Is(false), Arg.Is(migration3), Arg.Is(migration3.Items[2]));

		mockSet.MigrationItemDeployer.Received(2)
			.DeployMigrationItem(Arg.Is(false), Arg.Any<Migration>(), Arg.Any<MigrationItem>());

		mockSet.EasyFlowDA.Received(2)
			.SaveMigrationItemState(Arg.Any<MigrationItemDto>());

		mockSet.EasyFlowDA.Received().SaveMigrationItemState(Arg.Is(migrationItemDto1));

		mockSet.EasyFlowDA.Received().SaveMigrationItemState(Arg.Is(migrationItemDto2));

		Assert.Equal(appliedUtc1, migrationItemDto1.AppliedUtc);
		Assert.Equal(1555, migrationItemDto1.ExecutionTimeMs);

		Assert.Equal(appliedUtc2, migrationItemDto2.AppliedUtc);
		Assert.Equal(2555, migrationItemDto2.ExecutionTimeMs);
	}


	[Fact]
	public void DeployBreakingChanges_BadContentHash()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowDA.GetNonAppliedBreakingMigrationItems().Returns([
			new()
			{
				Version = 2,
				ContentHash = "-- content 2.breaking".Crc32HashCode(),
				Content = "-- content 2.breaking",
				ItemType = MigrationItemType.Breaking,
				Name = "second-migration",
				Status = MigrationItemStatus.Skipped
			}
		]);

		string changedContent = "-- content 2.breaking !!! CONTENT CHANGED, BAD HASH !!!";

		Migration migration2 = new()
		{
			FolderPath = "c:/db/migrations/002.second-migration",
			Name = "second-migration",
			Version = 2,
			Items = new List<MigrationItem>
					{
						new() {
							MigrationItemType = MigrationItemType.Migration,
							FileData = GetFileData("m.1.item.sql", "-- content 2.1")
						},
						new() {
							MigrationItemType = MigrationItemType.Breaking,
							FileData = GetFileData("breaking.sql", changedContent)
						},
					}.AsReadOnly()
		};

		mockSet.EasyFlowProject.GetMigrations().Returns([migration2]);

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act

		// Assert
		Assert.Throws<FileContentChangedException>(() => deploy.DeployBreakingChanges(DeployParameters.Breaking));
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