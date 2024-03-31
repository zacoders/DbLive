using EasyFlow.Adapter;
using EasyFlow.Deployers.Migrations;

namespace EasyFlow.Tests.Deployers.Migrations;

public class BreakingChangesDeployerTests
{
	[Fact]
	public void GetMigrationsToApply_SkipBreakingChangesDeployment()
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
	public void GetMigrationsToApply_NoBreaknigChangesItemssToApply()
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
	public void GetMigrationsToApply()
	{
		// Arrange
		MockSet mockSet = new();

		mockSet.EasyFlowDA.GetNonAppliedBreakingMigrationItems().Returns(
			new List<MigrationItemDto>
			{
				new() {
					Version = 2,
					ContentHash = "-- content 2.undo".Crc32HashCode(),
					Content = "-- content 2.undo",
					ItemType = "breaking",
					Name = "second-migration",
					Status = "skipped"
				},
				new() {
					Version = 3,
					ContentHash = "-- content 3.undo".Crc32HashCode(),
					Content = "-- content 3.undo",
					ItemType = "breaking",
					Name = "3rd-migration",
					Status = "skipped"
				}
			}.AsReadOnly()
		);

		mockSet.EasyFlowProject.GetMigrations().Returns(
			new List<Migration>
			{
				new() {
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
							MigrationItemType = MigrationItemType.Undo,
							FileData = GetFileData("undo.sql", "-- content 1.undo")
						},
					}.AsReadOnly()
				},
				new() {
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
							MigrationItemType = MigrationItemType.Undo,
							FileData = GetFileData("undo.sql", "-- content 2.undo")
						},
					}.AsReadOnly()
				},
				new() {
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
							MigrationItemType = MigrationItemType.Undo,
							FileData = GetFileData("undo.sql", "-- content 3.undo")
						},
					}.AsReadOnly()
				},
				new() {
					FolderPath = "c:/db/migrations/004.second-migration",
					Name = "second-migration",
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
							MigrationItemType = MigrationItemType.Undo,
							FileData = GetFileData("undo.sql", "-- content 4.undo")
						},
					}.AsReadOnly()
				}
			}
		);

		var deploy = mockSet.CreateUsingMocks<BreakingChangesDeployer>();

		// Act
		deploy.DeployBreakingChanges(DeployParameters.Breaking);

		// Assert
		mockSet.EasyFlowProject.Received().GetMigrations();
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